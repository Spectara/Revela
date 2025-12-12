"use strict";
/**
 * PinchZoom - Touch & Mouse Zoom/Pan für Bilder
 * @module PinchZoom
 */
// ==================== CONSTANTS ====================
const DEFAULT_OPTIONS = {
    minScale: 1,
    maxScale: 10,
    maxPixelRatio: 2,
    resetOnEnd: false,
    doubleTapZoom: true,
    doubleTapDelay: 300,
    panEnabled: true,
    swipeToClose: true,
    swipeThreshold: 80,
    mouseEnabled: true
};
const ZOOM_THRESHOLD = 1.01;
const SWIPE_PROGRESS_DIVISOR = 300;
const SWIPE_SCALE_FACTOR = 0.15;
const SWIPE_OFFSET_MULTIPLIER = 200;
const WHEEL_ZOOM_IN = 1.1;
const WHEEL_ZOOM_OUT = 0.9;
// ==================== CSS CLASSES ====================
const CssClass = {
    Zooming: "zooming",
    Swiping: "swiping",
    Dragging: "dragging"
};
const CssVariable = {
    ZoomScale: "--zoom-scale",
    ZoomX: "--zoom-x",
    ZoomY: "--zoom-y",
    SwipeY: "--swipe-y",
    SwipeScale: "--swipe-scale"
};
// ==================== UTILITIES ====================
const clamp = (value, min, max) => Math.min(Math.max(value, min), max);
const createPoint = (x, y) => ({ x, y });
const getCenter = (rect) => createPoint(rect.left + rect.width / 2, rect.top + rect.height / 2);
const getTouchMetrics = (touches) => {
    const [a, b] = [touches[0], touches[1]];
    const dx = a.clientX - b.clientX;
    const dy = a.clientY - b.clientY;
    return {
        distance: Math.hypot(dx, dy),
        midpoint: createPoint(a.clientX - dx / 2, a.clientY - dy / 2)
    };
};
// ==================== CLASS ====================
class PinchZoom {
    constructor(element, options = {}) {
        this._abortController = null;
        this._transform = { scale: 1, x: 0, y: 0 };
        this._gestureState = null;
        this._panState = null;
        this._swipeState = null;
        this._lastTapTime = 0;
        this._originalSize = null;
        this._isDragging = false;
        // ==================== MOUSE HANDLERS ====================
        this._onDoubleClick = (event) => {
            event.preventDefault();
            this._handleZoomToggle(createPoint(event.clientX, event.clientY));
        };
        this._onMouseDown = (event) => {
            var _a, _b;
            if (!this._isZoomed || !this._options.panEnabled || event.button !== 0)
                return;
            event.preventDefault();
            this._isDragging = true;
            (_a = this._figure) === null || _a === void 0 ? void 0 : _a.classList.add(CssClass.Dragging);
            this._panState = {
                startPoint: createPoint(event.clientX, event.clientY),
                startTranslate: createPoint(this._transform.x, this._transform.y)
            };
            (_b = this._figure) === null || _b === void 0 ? void 0 : _b.classList.add(CssClass.Zooming);
        };
        this._onMouseMove = (event) => {
            if (!this._isDragging || !this._panState)
                return;
            event.preventDefault();
            this._handlePan(createPoint(event.clientX, event.clientY));
        };
        this._onMouseUp = () => {
            var _a, _b;
            if (!this._isDragging)
                return;
            this._isDragging = false;
            this._panState = null;
            (_a = this._figure) === null || _a === void 0 ? void 0 : _a.classList.remove(CssClass.Dragging);
            if (!this._isZoomed) {
                (_b = this._figure) === null || _b === void 0 ? void 0 : _b.classList.remove(CssClass.Zooming);
                this._resetTransform();
            }
        };
        this._onWheel = (event) => {
            event.preventDefault();
            const rect = this._element.getBoundingClientRect();
            if (!this._isZoomed) {
                this._originalSize = { width: rect.width, height: rect.height };
            }
            const zoomFactor = event.deltaY > 0 ? WHEEL_ZOOM_OUT : WHEEL_ZOOM_IN;
            const newScale = clamp(this._transform.scale * zoomFactor, this._options.minScale, this._maxScale);
            if (newScale === this._transform.scale)
                return;
            const mousePoint = createPoint(event.clientX, event.clientY);
            const center = getCenter(rect);
            const scaleDelta = newScale / this._transform.scale;
            const zoomOffset = createPoint((mousePoint.x - center.x - this._transform.x) * (1 - scaleDelta), (mousePoint.y - center.y - this._transform.y) * (1 - scaleDelta));
            const { width, height } = this._getOriginalSize();
            this._transform = {
                scale: newScale,
                x: this._boundTranslate(this._transform.x + zoomOffset.x, newScale, width),
                y: this._boundTranslate(this._transform.y + zoomOffset.y, newScale, height)
            };
            this._updateZoomState();
            this._applyTransform();
        };
        // ==================== TOUCH HANDLERS ====================
        this._onTouchStart = (event) => {
            const { touches } = event;
            if (touches.length === 1) {
                this._handleSingleTouchStart(event, touches[0]);
                return;
            }
            if (touches.length === 2) {
                this._handlePinchStart(event, touches);
            }
        };
        this._onTouchMove = (event) => {
            const { touches } = event;
            if (touches.length === 1 && this._swipeState && !this._isZoomed) {
                this._handleSwipe(event, touches[0]);
                return;
            }
            if (touches.length === 1 && this._panState) {
                event.preventDefault();
                this._handlePan(createPoint(touches[0].clientX, touches[0].clientY));
                return;
            }
            if (touches.length === 2 && this._gestureState) {
                event.preventDefault();
                this._handlePinch(touches);
            }
        };
        this._onTouchEnd = (event) => {
            var _a;
            if (this._swipeState && event.touches.length === 0) {
                this._handleSwipeEnd(event.changedTouches[0]);
                return;
            }
            this._gestureState = null;
            this._panState = null;
            this._swipeState = null;
            if (this._options.resetOnEnd || !this._isZoomed) {
                (_a = this._figure) === null || _a === void 0 ? void 0 : _a.classList.remove(CssClass.Zooming);
                this._resetTransform();
            }
        };
        this._element = element;
        this._figure = element.closest("figure");
        this._article = element.closest("article");
        this._options = Object.assign(Object.assign({}, DEFAULT_OPTIONS), options);
        this._attach();
    }
    // ==================== LIFECYCLE ====================
    _attach() {
        this._abortController = new AbortController();
        const { signal } = this._abortController;
        const passive = { signal, passive: false };
        const active = { signal };
        // Touch Events
        this._element.addEventListener("touchstart", this._onTouchStart, passive);
        this._element.addEventListener("touchmove", this._onTouchMove, passive);
        this._element.addEventListener("touchend", this._onTouchEnd, active);
        this._element.addEventListener("touchcancel", this._onTouchEnd, active);
        // Mouse Events
        if (this._options.mouseEnabled) {
            this._element.addEventListener("dblclick", this._onDoubleClick, active);
            this._element.addEventListener("mousedown", this._onMouseDown, active);
            this._element.addEventListener("wheel", this._onWheel, passive);
            window.addEventListener("mousemove", this._onMouseMove, active);
            window.addEventListener("mouseup", this._onMouseUp, active);
        }
    }
    destroy() {
        var _a;
        (_a = this._abortController) === null || _a === void 0 ? void 0 : _a.abort();
        this._abortController = null;
        this._resetTransform();
    }
    // ==================== GETTERS ====================
    get _isZoomed() {
        return this._transform.scale > ZOOM_THRESHOLD;
    }
    get _maxScale() {
        const { naturalWidth, naturalHeight } = this._element;
        const { width, height } = this._getOriginalSize();
        if (!naturalWidth || !naturalHeight) {
            return this._options.maxScale;
        }
        const nativeScale = Math.min(naturalWidth / width, naturalHeight / height);
        return Math.min(nativeScale * this._options.maxPixelRatio, this._options.maxScale);
    }
    _getOriginalSize() {
        if (!this._originalSize) {
            const { width, height } = this._element.getBoundingClientRect();
            this._originalSize = { width, height };
        }
        return this._originalSize;
    }
    _handleSingleTouchStart(event, touch) {
        var _a;
        const now = Date.now();
        const point = createPoint(touch.clientX, touch.clientY);
        // Double-Tap Check
        if (this._options.doubleTapZoom &&
            now - this._lastTapTime < this._options.doubleTapDelay) {
            event.preventDefault();
            this._handleZoomToggle(point);
            this._lastTapTime = 0;
            return;
        }
        this._lastTapTime = now;
        // Pan (wenn gezoomt)
        if (this._isZoomed && this._options.panEnabled) {
            event.preventDefault();
            this._panState = {
                startPoint: point,
                startTranslate: createPoint(this._transform.x, this._transform.y)
            };
            (_a = this._figure) === null || _a === void 0 ? void 0 : _a.classList.add(CssClass.Zooming);
            return;
        }
        // Swipe-Detection
        if (!this._isZoomed && this._options.swipeToClose) {
            this._swipeState = { startPoint: point, startTime: now };
        }
    }
    _handlePinchStart(event, touches) {
        var _a;
        event.preventDefault();
        this._panState = null;
        this._swipeState = null;
        const metrics = getTouchMetrics(touches);
        const rect = this._element.getBoundingClientRect();
        if (!this._isZoomed) {
            this._originalSize = { width: rect.width, height: rect.height };
        }
        this._gestureState = {
            initialDistance: metrics.distance,
            initialMidpoint: metrics.midpoint,
            initialScale: this._transform.scale,
            initialTranslate: createPoint(this._transform.x, this._transform.y),
            initialCenter: getCenter(rect)
        };
        (_a = this._figure) === null || _a === void 0 ? void 0 : _a.classList.add(CssClass.Zooming);
    }
    _handleSwipe(event, touch) {
        var _a;
        const deltaY = touch.clientY - this._swipeState.startPoint.y;
        const deltaX = touch.clientX - this._swipeState.startPoint.x;
        if (Math.abs(deltaY) <= Math.abs(deltaX) || deltaY <= 0)
            return;
        event.preventDefault();
        const progress = clamp(deltaY / SWIPE_PROGRESS_DIVISOR, 0, 1);
        const scale = 1 - progress * SWIPE_SCALE_FACTOR;
        const offsetY = (1 - scale) * SWIPE_OFFSET_MULTIPLIER;
        this._element.style.setProperty(CssVariable.SwipeY, `${offsetY}px`);
        this._element.style.setProperty(CssVariable.SwipeScale, String(scale));
        (_a = this._figure) === null || _a === void 0 ? void 0 : _a.classList.add(CssClass.Swiping);
    }
    _handlePan(currentPoint) {
        if (!this._panState)
            return;
        const { startPoint, startTranslate } = this._panState;
        const { width, height } = this._getOriginalSize();
        this._transform = Object.assign(Object.assign({}, this._transform), { x: this._boundTranslate(startTranslate.x + currentPoint.x - startPoint.x, this._transform.scale, width), y: this._boundTranslate(startTranslate.y + currentPoint.y - startPoint.y, this._transform.scale, height) });
        this._applyTransform();
    }
    _handlePinch(touches) {
        const state = this._gestureState;
        const metrics = getTouchMetrics(touches);
        const { width, height } = this._getOriginalSize();
        const scaleRatio = metrics.distance / state.initialDistance;
        const newScale = clamp(state.initialScale * scaleRatio, this._options.minScale, this._maxScale);
        const scaleDelta = newScale / state.initialScale;
        const zoomOffset = createPoint((state.initialMidpoint.x - state.initialCenter.x) * (1 - scaleDelta), (state.initialMidpoint.y - state.initialCenter.y) * (1 - scaleDelta));
        const pan = createPoint(metrics.midpoint.x - state.initialMidpoint.x, metrics.midpoint.y - state.initialMidpoint.y);
        this._transform = {
            scale: newScale,
            x: this._boundTranslate(state.initialTranslate.x + zoomOffset.x + pan.x, newScale, width),
            y: this._boundTranslate(state.initialTranslate.y + zoomOffset.y + pan.y, newScale, height)
        };
        this._applyTransform();
    }
    _handleSwipeEnd(touch) {
        var _a, _b;
        const state = this._swipeState;
        const deltaY = touch.clientY - state.startPoint.y;
        const deltaX = touch.clientX - state.startPoint.x;
        (_a = this._figure) === null || _a === void 0 ? void 0 : _a.classList.remove(CssClass.Swiping);
        this._element.style.removeProperty(CssVariable.SwipeY);
        this._element.style.removeProperty(CssVariable.SwipeScale);
        if (deltaY > this._options.swipeThreshold &&
            Math.abs(deltaY) > Math.abs(deltaX)) {
            (_b = this._article) === null || _b === void 0 ? void 0 : _b.blur();
        }
        this._swipeState = null;
    }
    // ==================== ZOOM LOGIC ====================
    _handleZoomToggle(point) {
        var _a, _b;
        if (this._isZoomed) {
            (_a = this._figure) === null || _a === void 0 ? void 0 : _a.classList.remove(CssClass.Zooming);
            this._resetTransform();
            return;
        }
        const rect = this._element.getBoundingClientRect();
        this._originalSize = { width: rect.width, height: rect.height };
        const center = getCenter(rect);
        const targetScale = this._maxScale;
        const zoomOffset = createPoint((point.x - center.x) * (1 - targetScale), (point.y - center.y) * (1 - targetScale));
        const { width, height } = this._originalSize;
        this._transform = {
            scale: targetScale,
            x: this._boundTranslate(zoomOffset.x, targetScale, width),
            y: this._boundTranslate(zoomOffset.y, targetScale, height)
        };
        (_b = this._figure) === null || _b === void 0 ? void 0 : _b.classList.add(CssClass.Zooming);
        this._applyTransform();
    }
    _updateZoomState() {
        var _a, _b;
        if (this._isZoomed) {
            (_a = this._figure) === null || _a === void 0 ? void 0 : _a.classList.add(CssClass.Zooming);
        }
        else {
            (_b = this._figure) === null || _b === void 0 ? void 0 : _b.classList.remove(CssClass.Zooming);
            this._resetTransform();
        }
    }
    // ==================== TRANSFORM ====================
    _boundTranslate(value, scale, size) {
        const maxOffset = ((scale - 1) * size) / 2;
        return clamp(value, -maxOffset, maxOffset);
    }
    _applyTransform() {
        const { scale, x, y } = this._transform;
        const { style } = this._element;
        style.setProperty(CssVariable.ZoomScale, String(scale));
        style.setProperty(CssVariable.ZoomX, `${x}px`);
        style.setProperty(CssVariable.ZoomY, `${y}px`);
    }
    _resetTransform() {
        this._transform = { scale: 1, x: 0, y: 0 };
        this._originalSize = null;
        this._element.removeAttribute("style");
    }
}
// ==================== PUBLIC API ====================
const instances = new WeakMap();
/**
 * Initialisiert PinchZoom für alle Elemente die dem Selektor entsprechen
 */
const initPinchZoom = (selector = "main .gallery article figure img", options = {}) => {
    document.querySelectorAll(selector).forEach((element) => {
        if (!instances.has(element)) {
            instances.set(element, new PinchZoom(element, options));
        }
    });
};
/**
 * Entfernt PinchZoom von einem Element
 */
const destroyPinchZoom = (element) => {
    var _a;
    (_a = instances.get(element)) === null || _a === void 0 ? void 0 : _a.destroy();
    instances.delete(element);
};
// ==================== INIT ====================
const init = () => {
    initPinchZoom("main .gallery article figure img", {
        maxPixelRatio: 2,
        resetOnEnd: false,
        swipeThreshold: 80
    });
};
if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
}
else {
    init();
}
// Global verfügbar machen (für CodePen / non-module Umgebung)
window.PinchZoom = {
    init: initPinchZoom,
    destroy: destroyPinchZoom
};
