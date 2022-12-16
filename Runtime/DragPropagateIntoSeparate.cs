using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Views
{
    /// <summary>
    ///   Properly propagate input from one canvas into another.
    /// </summary>
    public class DragPropagateIntoSeparate : MonoBehaviour,
        IPointerEnterHandler,
        IEventSystemHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler,
        IInitializePotentialDragHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IDropHandler,
        IScrollHandler
    {
        [SerializeField] private Canvas _originalCanvas = null!;
        [SerializeField] private Canvas _targetCanvas = null!;
        [SerializeField] private GraphicRaycaster _raycaster = null!;


        private List<RaycastResult> _list = new(20);
        private RectTransform _originalRectTransform;

        private Dictionary<int, PointerEventData> _events = new(10);

        private void Awake()
        {
            _originalRectTransform = _originalCanvas.GetComponent<RectTransform>();
        }

        private PointerEventData PropagatedDataFromOther(PointerEventData data)
        {
            if (!_events.TryGetValue(data.pointerId, out var customData))
            {
                customData = new PointerEventData(EventSystem.current);
                _events.Add(data.pointerId, customData);
            }

            return customData;
        }

        private Vector2 CanvasToScreenPosition(Vector2 position)
        {
            var scaling = ScalingFactor();
            scaling.x = 1 / scaling.x;
            scaling.y = 1 / scaling.y;

            return Vector2.Scale(position, scaling) * _targetCanvas.scaleFactor;
        }

        private GameObject? Propagate<T>(
            string nameOfEvent,
            PointerEventData customData,
            ExecuteEvents.EventFunction<T> function,
            GameObject? usingAsRoot)
            where T : IEventSystemHandler
        {
            bool usedRaycast = false;


            if (usingAsRoot == null)
            {
                var positionBefore = customData.position;
                customData.position = CanvasToScreenPosition(customData.position);
                _raycaster.Raycast(customData, _list);
                customData.position = positionBefore;

                usingAsRoot = _list.FirstOrDefault().gameObject;
                usedRaycast = true;
            }

            _list.Clear();
            if (usingAsRoot == null)
            {
                usingAsRoot = _targetCanvas.gameObject;
            }

            bool foundNoRaycasts = usingAsRoot == _targetCanvas.gameObject;
            var foundObject = ExecuteEvents.ExecuteHierarchy(usingAsRoot, customData, function);

            Debug.LogWarning(
                $"{nameOfEvent} used raycast {usedRaycast} raycast sucess{!foundNoRaycasts} Found ? {(foundObject != null ? foundObject.name : null)}");

            return foundObject;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var customData = PropagatedDataFromOther(eventData);
            NewEnterFrom(eventData);

            eventData.Use();
            customData.Use();
        }

        private void NewEnterFrom(PointerEventData eventData)
        {
            var customData = PropagatedDataFromOther(eventData);
            customData.position = ScaledPosition(eventData.position);
            CopyTo(eventData, customData);
            var newEnter = Propagate(nameof(ExecuteEvents.pointerEnterHandler),
                customData,
                ExecuteEvents.pointerEnterHandler,
                null)!;

            if (newEnter == null)
            {
                newEnter = customData.pointerCurrentRaycast.gameObject;
            }

            customData.pointerEnter = newEnter;

            if (customData.pointerEnter != null && !customData.hovered.Contains(customData.pointerEnter))
            {
                customData.hovered.Add(customData.pointerEnter);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            var customData = PropagatedDataFromOther(eventData);
            CopyTo(eventData, customData);

            if (customData.pointerEnter != null)
                customData.hovered.RemoveAll(x => x == customData.pointerEnter);

            Propagate(nameof(ExecuteEvents.pointerExitHandler),
                customData,
                ExecuteEvents.pointerExitHandler,
                customData.pointerEnter);

            if (customData.pointerEnter != null)
                customData.hovered.RemoveAll(x => x == customData.pointerEnter);

            customData.pointerEnter = null!;

            eventData.Use();
            customData.Use();
        }

        private void PointerExitHovered(PointerEventData eventData, GameObject? toExitOn)
        {
            if (toExitOn == null)
                return;

            var customData = PropagatedDataFromOther(eventData);
            Propagate(nameof(ExecuteEvents.pointerExitHandler),
                customData,
                ExecuteEvents.pointerExitHandler,
                toExitOn);

            customData.hovered.RemoveAll(x => x == toExitOn);
            if (customData.pointerEnter == toExitOn)
            {
                customData.pointerEnter = null!;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            var customData = PropagatedDataFromOther(eventData);
            eventData.useDragThreshold = true;
            eventData.delta = Vector2.zero;
            eventData.dragging = false;
            eventData.useDragThreshold = true;
            eventData.scrollDelta = Vector2.zero;
            eventData.eligibleForClick = false;
            customData.dragging = false;
            customData.delta = Vector2.zero;
            customData.scrollDelta = Vector2.zero;

            CopyTo(eventData, customData);
            customData.position = ScaledPosition(eventData.position);
            customData.selectedObject = Propagate(nameof(ExecuteEvents.selectHandler),
                customData,
                ExecuteEvents.selectHandler,
                null);

            customData.pointerPress = Propagate(nameof(ExecuteEvents.pointerDownHandler),
                customData,
                ExecuteEvents.pointerDownHandler,
                customData.pointerCurrentRaycast.gameObject);

            customData.pointerPressRaycast = customData.pointerCurrentRaycast;
            customData.rawPointerPress = customData.pointerCurrentRaycast.gameObject;
            customData.pressPosition = customData.pointerPressRaycast.screenPosition;

            eventData.dragging = false;
            eventData.delta = Vector2.zero;
            eventData.scrollDelta = Vector2.zero;
            customData.dragging = false;
            customData.delta = Vector2.zero;
            customData.scrollDelta = Vector2.zero;
            customData.useDragThreshold = false;

            OnBeginDrag(eventData);
            OnEndDrag(eventData);

            customData.eligibleForClick = customData.pointerPress != null;
            eventData.Use();
            customData.Use();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            var customData = PropagatedDataFromOther(eventData);
            CopyTo(eventData, customData);
            customData.useDragThreshold = true;

            Propagate(nameof(ExecuteEvents.pointerUpHandler),
                customData,
                ExecuteEvents.pointerUpHandler,
                customData.pointerPress);

            foreach (var hovered in customData.hovered.ToArray())
            {
                if (customData.pointerCurrentRaycast.gameObject != null
                    && hovered != customData.pointerCurrentRaycast.gameObject)
                    PointerExitHovered(eventData, hovered);
            }

            PointerExitHovered(eventData, customData.pointerPress);
            Propagate(nameof(ExecuteEvents.deselectHandler),
                customData,
                ExecuteEvents.deselectHandler,
                customData.selectedObject);

            customData.selectedObject = null;
            customData.eligibleForClick = false;
            customData.pointerPress = null;
            customData.rawPointerPress = null;
            customData.pointerClick = null;
            customData.dragging = false;
            customData.pointerDrag = null;
            customData.pointerPressRaycast = default;

            OnPointerExit(eventData);

            eventData.Use();
            customData.Use();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            var customData = PropagatedDataFromOther(eventData);

            if (customData.eligibleForClick)
            {
                Propagate(nameof(ExecuteEvents.pointerClickHandler),
                    customData,
                    ExecuteEvents.pointerClickHandler,
                    customData.pointerCurrentRaycast.gameObject);
            }

            customData.pointerPressRaycast = default;

            eventData.Use();
            customData.Use();
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            var customData = PropagatedDataFromOther(eventData);
            eventData.Use();

            customData.pointerDrag = Propagate(nameof(ExecuteEvents.initializePotentialDrag),
                customData,
                ExecuteEvents.initializePotentialDrag,
                customData.pointerCurrentRaycast.gameObject);

            customData.Use();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            var customData = PropagatedDataFromOther(eventData);
            CopyTo(eventData, customData);

            customData.pointerDrag = Propagate(nameof(ExecuteEvents.beginDragHandler),
                customData,
                ExecuteEvents.beginDragHandler,
                customData.pointerDrag);


            eventData.Use();
            customData.Use();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.delta == Vector2.zero)
                return;

            var customData = PropagatedDataFromOther(eventData);
            customData.position = ScaledPosition(eventData.position);
            CopyTo(eventData, customData);

            Propagate(nameof(ExecuteEvents.dragHandler),
                customData,
                ExecuteEvents.dragHandler,
                customData.pointerDrag);

            eventData.Use();
            customData.Use();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            var customData = PropagatedDataFromOther(eventData);
            CopyTo(eventData, customData);

            Propagate(nameof(ExecuteEvents.endDragHandler),
                customData,
                ExecuteEvents.endDragHandler,
                customData.pointerDrag);


            customData.pointerDrag = null;
            customData.delta = Vector2.zero;
            customData.dragging = false;

            eventData.Use();
            customData.Use();
        }

        public void OnDrop(PointerEventData eventData)
        {
            var customData = PropagatedDataFromOther(eventData);
            CopyTo(eventData, customData);

            Propagate(nameof(ExecuteEvents.dropHandler),
                customData,
                ExecuteEvents.dropHandler,
                customData.pointerPressRaycast.gameObject);

            eventData.Use();
            customData.Use();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (eventData.scrollDelta == Vector2.zero)
                return;

            var customData = PropagatedDataFromOther(eventData);
            customData.scrollDelta = eventData.scrollDelta;

            Propagate(nameof(ExecuteEvents.scrollHandler),
                customData,
                ExecuteEvents.scrollHandler,
                customData.pointerCurrentRaycast.gameObject);

            eventData.Use();
            customData.Use();
        }

        private void CopyTo(PointerEventData from, PointerEventData copyTo)
        {
            var vectorScale = ScalingFactor();
            copyTo.button = from.button;
            copyTo.delta = Vector2.Scale(from.delta, vectorScale);
            copyTo.dragging = from.dragging;
            copyTo.pressure = from.pressure;
            copyTo.twist = from.twist;
            copyTo.altitudeAngle = from.altitudeAngle;
            copyTo.azimuthAngle = from.azimuthAngle;
        }

        private Vector2 ScaledPosition(Vector2 original)
        {
            var vectorScale = ScalingFactor();
            var positionInRectangle = original;
            if (_originalCanvas.renderMode == RenderMode.WorldSpace)
            {
                // For WorldScale canvases  - we also need to figure out the correct position inside the canvas
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_originalRectTransform, original,
                    _originalCanvas.worldCamera, out positionInRectangle);
            }

            return Vector2.Scale(positionInRectangle, vectorScale);
        }

        private Vector2 ScalingFactor()
        {
            var targetWorldScale = _targetCanvas.transform.lossyScale;
            var originalWorldScale = _originalCanvas.transform.lossyScale;
            var scaleFactorX = targetWorldScale.x / originalWorldScale.x;
            var scaleFactorY = targetWorldScale.y / originalWorldScale.y;
            return new Vector2(scaleFactorX, scaleFactorY);
        }
    }
}