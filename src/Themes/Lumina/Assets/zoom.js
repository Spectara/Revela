// Zoom for the photo viewer image on photo pages and in lightbox dialogs.
//
// Browsers can zoom only the whole page, not a single element, so the image is
// scaled with a CSS transform. This script only sets the custom properties
// --zoom-scale, --zoom-x and --zoom-y plus two state attributes (data-zoomed,
// data-gesture); main.css turns them into the transform and its animation.
// Without JavaScript the photo is shown fitted to the viewport.
//
// Zoom goes up to 100 %: one image pixel per device pixel. Beyond that a photo
// only shows enlarged pixels. Mouse and pen: click toggles 100 %, drag pans.
// Touch: double-tap toggles 100 %, two fingers pinch (a pinch past 100 % or
// below the fitted size springs back on release), one finger pans while zoomed.
(() => {
  const images = document.querySelectorAll(
    "body.photo-page > main > article > picture > img, dialog[data-lightbox] > article > picture > img"
  );
  if (images.length === 0) {
    return;
  }

  const DOUBLE_TAP_MS = 300;
  const CLICK_SLOP_PX = 3;
  // Below this a zoom would be barely visible, so the photo is not zoomable.
  const MIN_ZOOM = 1.1;
  // Furthest a pinch can stretch past the limits, as a share of the limit.
  const MAX_OVERSTRETCH = 0.25;

  const clamp = (value, minimum, maximum) => Math.min(Math.max(value, minimum), maximum);
  // Past the limits a pinch meets growing resistance and never gets further than MAX_OVERSTRETCH.
  const resist = (scale, minimum, maximum) => {
    if (scale > maximum) {
      return maximum * (1 + MAX_OVERSTRETCH * (1 - Math.exp(1 - scale / maximum)));
    }

    return scale < minimum ? minimum * (1 - MAX_OVERSTRETCH * (1 - Math.exp(1 - minimum / scale))) : scale;
  };
  const point = (x, y) => ({ x, y });
  const touchMetrics = (touches) => {
    const first = touches[0];
    const second = touches[1];
    const deltaX = first.clientX - second.clientX;
    const deltaY = first.clientY - second.clientY;

    return {
      distance: Math.hypot(deltaX, deltaY),
      midpoint: point(first.clientX - deltaX / 2, first.clientY - deltaY / 2)
    };
  };

  class PhotoZoom {
    constructor(image) {
      this.image = image;
      this.picture = image.parentElement;
      this.transform = { scale: 1, x: 0, y: 0 };
      this.baseSize = null;
      this.press = null;
      this.pan = null;
      this.pinch = null;
      this.lastTapTime = Number.NEGATIVE_INFINITY;
      this.fullResolutionRequested = false;
      this.originalPreload = null;

      // Mouse and pen use Pointer Events with pointer capture, so a drag that
      // leaves the image keeps panning without document-wide listeners.
      image.addEventListener("pointerdown", (event) => this.onPointerDown(event));
      image.addEventListener("pointermove", (event) => this.onPointerMove(event));
      image.addEventListener("pointerup", (event) => this.onPointerUp(event));
      image.addEventListener("pointercancel", () => this.endPress());

      // Touch uses Touch Events: cancelling touchstart is what reliably keeps
      // mobile browsers (notably iOS Safari) from zooming the page instead.
      const cancelable = { passive: false };
      image.addEventListener("touchstart", (event) => this.onTouchStart(event), cancelable);
      image.addEventListener("touchmove", (event) => this.onTouchMove(event), cancelable);
      image.addEventListener("touchend", (event) => this.onTouchEnd(event));
      image.addEventListener("touchcancel", (event) => this.onTouchEnd(event));

      image.closest("dialog")?.addEventListener("close", () => this.reset());
    }

    get isZoomed() {
      return this.transform.scale > 1.01;
    }

    // Scale at which one image pixel covers one device pixel (100 %).
    get fullScale() {
      const { width, height } = this.getBaseSize();
      const sourceWidth = Number.parseFloat(this.image.getAttribute("width")) || this.image.naturalWidth;
      const sourceHeight = Number.parseFloat(this.image.getAttribute("height")) || this.image.naturalHeight;
      if (!sourceWidth || !sourceHeight || !width || !height) {
        return 1;
      }

      return Math.min(sourceWidth / width, sourceHeight / height) / (window.devicePixelRatio || 1);
    }

    get canZoom() {
      return this.fullScale >= MIN_ZOOM;
    }

    onPointerDown(event) {
      if (event.pointerType === "touch" || event.button !== 0) {
        return;
      }

      // No native image drag or text selection.
      event.preventDefault();
      this.image.setPointerCapture(event.pointerId);
      this.press = point(event.clientX, event.clientY);
      if (this.isZoomed) {
        this.startPan(this.press);
      }
    }

    onPointerMove(event) {
      if (event.pointerType !== "touch" && this.pan) {
        this.updatePan(point(event.clientX, event.clientY));
      }
    }

    onPointerUp(event) {
      if (event.pointerType === "touch" || !this.press) {
        return;
      }

      const release = point(event.clientX, event.clientY);
      const isClick = Math.hypot(release.x - this.press.x, release.y - this.press.y) <= CLICK_SLOP_PX;
      this.endPress();
      if (isClick) {
        this.toggle(release);
      }
    }

    endPress() {
      this.press = null;
      this.pan = null;
      this.setGesture(false);
    }

    onTouchStart(event) {
      if (event.touches.length === 2) {
        event.preventDefault();
        this.startPinch(event.touches);
        return;
      }

      if (event.touches.length !== 1) {
        return;
      }

      const touch = event.touches[0];
      const tap = point(touch.clientX, touch.clientY);
      const now = performance.now();
      if (now - this.lastTapTime < DOUBLE_TAP_MS) {
        event.preventDefault();
        this.lastTapTime = Number.NEGATIVE_INFINITY;
        this.toggle(tap);
        return;
      }

      this.lastTapTime = now;
      if (this.isZoomed) {
        event.preventDefault();
        this.startPan(tap);
      }
    }

    onTouchMove(event) {
      if (event.touches.length === 2 && this.pinch) {
        event.preventDefault();
        this.updatePinch(event.touches);
      } else if (event.touches.length === 1 && this.pan) {
        event.preventDefault();
        this.updatePan(point(event.touches[0].clientX, event.touches[0].clientY));
      }
    }

    onTouchEnd(event) {
      if (event.touches.length < 2 && this.pinch) {
        const focus = this.pinch.focus;
        this.pinch = null;
        this.settle(focus);
      }

      if (event.touches.length === 0) {
        this.pan = null;
        this.setGesture(false);
      }
    }

    startPan(start) {
      this.pan = { start, translate: point(this.transform.x, this.transform.y) };
      this.setGesture(true);
    }

    startPinch(touches) {
      this.loadFullResolution();
      const metrics = touchMetrics(touches);
      const rect = this.image.getBoundingClientRect();
      this.getBaseSize();
      this.pinch = {
        distance: metrics.distance,
        midpoint: metrics.midpoint,
        focus: metrics.midpoint,
        scale: this.transform.scale,
        translate: point(this.transform.x, this.transform.y),
        center: point(rect.left + rect.width / 2, rect.top + rect.height / 2)
      };
      this.pan = null;
      this.setGesture(true);
    }

    updatePinch(touches) {
      const metrics = touchMetrics(touches);
      this.pinch.focus = metrics.midpoint;
      const scale = resist(this.pinch.scale * (metrics.distance / this.pinch.distance), 1, Math.max(this.fullScale, 1));
      const scaleDelta = scale / this.pinch.scale;
      const pan = point(metrics.midpoint.x - this.pinch.midpoint.x, metrics.midpoint.y - this.pinch.midpoint.y);
      const zoomOffset = point(
        (this.pinch.midpoint.x - this.pinch.center.x) * (1 - scaleDelta),
        (this.pinch.midpoint.y - this.pinch.center.y) * (1 - scaleDelta)
      );

      this.transform = {
        scale,
        x: this.bound(this.pinch.translate.x + zoomOffset.x + pan.x, scale, "width"),
        y: this.bound(this.pinch.translate.y + zoomOffset.y + pan.y, scale, "height")
      };
      this.apply();
    }

    updatePan(current) {
      const { scale } = this.transform;
      this.transform = {
        scale,
        x: this.bound(this.pan.translate.x + current.x - this.pan.start.x, scale, "width"),
        y: this.bound(this.pan.translate.y + current.y - this.pan.start.y, scale, "height")
      };
      this.apply();
    }

    // Zooms in to 100 % around the given viewport point, or back out when zoomed.
    toggle(origin) {
      if (this.isZoomed) {
        this.reset();
        return;
      }

      this.baseSize = { width: this.image.clientWidth, height: this.image.clientHeight };
      if (!this.canZoom) {
        return;
      }

      this.loadFullResolution();
      const rect = this.image.getBoundingClientRect();
      const scale = this.fullScale;
      const center = point(rect.left + rect.width / 2, rect.top + rect.height / 2);
      this.transform = {
        scale,
        x: this.bound((origin.x - center.x) * (1 - scale), scale, "width"),
        y: this.bound((origin.y - center.y) * (1 - scale), scale, "height")
      };
      this.apply();
    }

    // Brings a pinch that went past 100 % or below the fitted size back into
    // range, scaling around the point between the fingers so it stays put.
    settle(focus) {
      const { scale: current, x, y } = this.transform;
      const scale = clamp(current, 1, Math.max(this.fullScale, 1));
      if (scale <= 1.01) {
        this.reset();
        return;
      }

      // The transform scales around the image centre, which the translation moves.
      const rect = this.image.getBoundingClientRect();
      const centre = point(rect.left + rect.width / 2 - x, rect.top + rect.height / 2 - y);
      const ratio = scale / current;
      this.setGesture(false);
      this.transform = {
        scale,
        x: this.bound((focus.x - centre.x) * (1 - ratio) + x * ratio, scale, "width"),
        y: this.bound((focus.y - centre.y) * (1 - ratio) + y * ratio, scale, "height")
      };
      this.apply();
    }

    // Keeps the scaled image covering its unscaled box, so no empty edge shows.
    bound(value, scale, dimension) {
      const limit = (Math.max(scale - 1, 0) * this.getBaseSize()[dimension]) / 2;
      return clamp(value, -limit, limit);
    }

    // The viewer first shows a viewport-sized variant; zooming asks for the
    // widest candidate (the original). Switching the visible image right away
    // would blank it until the original arrives, so a detached copy of the
    // <picture> fetches and decodes it first; the visible image switches
    // afterwards and gets the already loaded original without a gap.
    loadFullResolution() {
      const sourceWidth = this.image.getAttribute("width");
      if (this.fullResolutionRequested || !sourceWidth) {
        return;
      }

      const useOriginal = (picture) => {
        for (const candidate of picture.querySelectorAll(":scope > source[sizes], :scope > img[sizes]")) {
          candidate.sizes = `${sourceWidth}px`;
        }
      };

      this.fullResolutionRequested = true;
      // Referenced until the switch so the detached copy, and its download, stay alive.
      this.originalPreload = this.picture.cloneNode(true);
      const preloadImage = this.originalPreload.querySelector("img");
      preloadImage.loading = "eager";
      preloadImage.fetchPriority = "high";
      useOriginal(this.originalPreload);
      preloadImage.decode().catch(() => {}).then(() => {
        useOriginal(this.picture);
        this.originalPreload = null;
      });
    }

    getBaseSize() {
      this.baseSize ??= { width: this.image.clientWidth, height: this.image.clientHeight };
      return this.baseSize;
    }

    apply() {
      this.image.toggleAttribute("data-zoomed", this.isZoomed);
      this.image.style.setProperty("--zoom-scale", String(this.transform.scale));
      this.image.style.setProperty("--zoom-x", `${this.transform.x}px`);
      this.image.style.setProperty("--zoom-y", `${this.transform.y}px`);
    }

    // Shows the zoom cursor only where zooming does something; the fitted size
    // changes with layout, the window size and when a lightbox opens.
    updateZoomable() {
      if (!this.isZoomed) {
        this.baseSize = null;
      }

      this.image.toggleAttribute("data-zoomable", this.canZoom);
    }

    // While a finger or the mouse moves the image, CSS skips the transition.
    setGesture(active) {
      this.image.toggleAttribute("data-gesture", active);
    }

    reset() {
      this.transform = { scale: 1, x: 0, y: 0 };
      this.baseSize = null;
      this.pinch = null;
      this.endPress();
      this.image.removeAttribute("data-zoomed");
      for (const property of ["--zoom-scale", "--zoom-x", "--zoom-y"]) {
        this.image.style.removeProperty(property);
      }
    }
  }

  const viewers = new Map(Array.from(images, (image) => [image, new PhotoZoom(image)]));
  const fittedSize = new ResizeObserver((entries) => {
    for (const entry of entries) {
      viewers.get(entry.target).updateZoomable();
    }
  });
  for (const image of viewers.keys()) {
    fittedSize.observe(image);
  }

  // A new viewport size changes the fitted image size that zoom is based on.
  window.addEventListener("resize", () => {
    for (const viewer of viewers.values()) {
      viewer.reset();
    }
  });
})();
