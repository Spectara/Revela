(() => {
  const targets = document.querySelectorAll(
    'body.photo-page > main > article > picture > img, dialog[data-lightbox] > article > picture > img'
  );

  if (targets.length === 0) {
    return;
  }

  const clamp = (value, minimum, maximum) => Math.min(Math.max(value, minimum), maximum);
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

  class PinchZoom {
    constructor(image) {
      this.image = image;
      this.stage = image.parentElement;
      this.dialog = image.closest("dialog");
      this.abortController = new AbortController();
      this.transform = { scale: 1, x: 0, y: 0 };
      this.gesture = null;
      this.pan = null;
      this.lastTapTime = 0;
      this.baseSize = null;
      this.pointerStart = null;
      this.hasFullResolution = false;
      this.preload = null;

      this.attach();
    }

    get isZoomed() {
      return this.transform.scale > 1.01;
    }

    get maxScale() {
      const { width, height } = this.getBaseSize();
      const sourceWidth = Number.parseFloat(this.image.getAttribute("width")) || this.image.naturalWidth;
      const sourceHeight = Number.parseFloat(this.image.getAttribute("height")) || this.image.naturalHeight;
      if (!sourceWidth || !sourceHeight || !width || !height) {
        return 10;
      }

      const nativeScale = Math.min(
        sourceWidth / width,
        sourceHeight / height
      );
      return clamp(nativeScale * 2, 1, 10);
    }

    attach() {
      const signal = this.abortController.signal;
      const active = { signal };
      const cancelable = { signal, passive: false };

      this.image.addEventListener("touchstart", (event) => this.onTouchStart(event), cancelable);
      this.image.addEventListener("touchmove", (event) => this.onTouchMove(event), cancelable);
      this.image.addEventListener("touchend", (event) => this.onTouchEnd(event), active);
      this.image.addEventListener("touchcancel", (event) => this.onTouchEnd(event), active);
      this.image.addEventListener("pointerdown", (event) => this.onPointerDown(event), active);
      this.image.addEventListener("pointerup", (event) => this.onPointerUp(event), active);
      this.image.addEventListener("pointercancel", () => this.onPointerCancel(), active);
      this.image.addEventListener("mousedown", (event) => this.onMouseDown(event), active);
      window.addEventListener("mousemove", (event) => this.onMouseMove(event), active);
      window.addEventListener("mouseup", () => this.onMouseUp(), active);
      window.addEventListener("resize", () => this.reset(), active);
      this.dialog?.addEventListener("close", () => this.reset(), active);
    }

    onPointerDown(event) {
      if (event.pointerType === "touch" || event.button !== 0) {
        return;
      }

      this.pointerStart = point(event.clientX, event.clientY);
    }

    onPointerUp(event) {
      if (event.pointerType === "touch" || event.button !== 0 || !this.pointerStart) {
        return;
      }

      const currentPoint = point(event.clientX, event.clientY);
      const movement = Math.hypot(
        currentPoint.x - this.pointerStart.x,
        currentPoint.y - this.pointerStart.y
      );
      this.pointerStart = null;
      if (movement > 3) {
        return;
      }

      event.preventDefault();
      this.toggle(currentPoint);
    }

    onPointerCancel() {
      this.pointerStart = null;
    }

    onMouseDown(event) {
      if (!this.isZoomed || event.button !== 0) {
        return;
      }

      event.preventDefault();
      this.pan = {
        start: point(event.clientX, event.clientY),
        translate: point(this.transform.x, this.transform.y)
      };
      this.image.dataset.dragging = "";
    }

    onMouseMove(event) {
      if (!this.pan) {
        return;
      }

      event.preventDefault();
      this.updatePan(point(event.clientX, event.clientY));
    }

    onMouseUp() {
      this.pan = null;
      delete this.image.dataset.dragging;
    }

    onTouchStart(event) {
      if (event.touches.length === 2) {
        event.preventDefault();
        this.loadFullResolution();
        const metrics = touchMetrics(event.touches);
        const rect = this.image.getBoundingClientRect();
        this.getBaseSize();
        this.gesture = {
          distance: metrics.distance,
          midpoint: metrics.midpoint,
          scale: this.transform.scale,
          translate: point(this.transform.x, this.transform.y),
          center: point(rect.left + rect.width / 2, rect.top + rect.height / 2)
        };
        this.pan = null;
        return;
      }

      if (event.touches.length !== 1) {
        return;
      }

      const now = Date.now();
      const touch = event.touches[0];
      const currentPoint = point(touch.clientX, touch.clientY);
      if (now - this.lastTapTime < 300) {
        event.preventDefault();
        this.toggle(currentPoint);
        this.lastTapTime = 0;
        return;
      }

      this.lastTapTime = now;
      if (this.isZoomed) {
        event.preventDefault();
        this.pan = {
          start: currentPoint,
          translate: point(this.transform.x, this.transform.y)
        };
      }
    }

    onTouchMove(event) {
      if (event.touches.length === 2 && this.gesture) {
        event.preventDefault();
        this.updatePinch(event.touches);
        return;
      }

      if (event.touches.length === 1 && this.pan) {
        event.preventDefault();
        this.updatePan(point(event.touches[0].clientX, event.touches[0].clientY));
      }
    }

    onTouchEnd(event) {
      if (event.touches.length < 2) {
        this.gesture = null;
      }

      if (event.touches.length === 0) {
        this.pan = null;
      }
    }

    updatePinch(touches) {
      const metrics = touchMetrics(touches);
      const scale = clamp(
        this.gesture.scale * (metrics.distance / this.gesture.distance),
        1,
        this.maxScale
      );
      const scaleDelta = scale / this.gesture.scale;
      const pan = point(
        metrics.midpoint.x - this.gesture.midpoint.x,
        metrics.midpoint.y - this.gesture.midpoint.y
      );
      const zoomOffset = point(
        (this.gesture.midpoint.x - this.gesture.center.x) * (1 - scaleDelta),
        (this.gesture.midpoint.y - this.gesture.center.y) * (1 - scaleDelta)
      );

      this.transform = {
        scale,
        x: this.bound(this.gesture.translate.x + zoomOffset.x + pan.x, scale, "width"),
        y: this.bound(this.gesture.translate.y + zoomOffset.y + pan.y, scale, "height")
      };
      this.apply();
    }

    updatePan(currentPoint) {
      const deltaX = currentPoint.x - this.pan.start.x;
      const deltaY = currentPoint.y - this.pan.start.y;
      this.transform = {
        ...this.transform,
        x: this.bound(
          this.pan.translate.x + deltaX,
          this.transform.scale,
          "width"
        ),
        y: this.bound(
          this.pan.translate.y + deltaY,
          this.transform.scale,
          "height"
        )
      };
      this.apply();
    }

    toggle(currentPoint) {
      if (this.isZoomed) {
        this.reset();
        return;
      }

      this.loadFullResolution();
      const rect = this.image.getBoundingClientRect();
      this.baseSize = { width: this.image.clientWidth, height: this.image.clientHeight };
      const scale = this.maxScale;
      const center = point(rect.left + rect.width / 2, rect.top + rect.height / 2);
      this.transform = {
        scale,
        x: this.bound((currentPoint.x - center.x) * (1 - scale), scale, "width"),
        y: this.bound((currentPoint.y - center.y) * (1 - scale), scale, "height")
      };
      this.apply();
    }

    bound(value, scale, dimension) {
      const size = this.getBaseSize()[dimension];
      return clamp(value, -((scale - 1) * size) / 2, ((scale - 1) * size) / 2);
    }

    // The viewer initially picks a viewport-sized variant; zooming asks the
    // browser for the widest candidate (the original) instead. Switching the
    // visible image's sizes right away blanks it until the original arrives, so
    // the original is fetched and decoded on a detached copy of the <picture>
    // first; the visible image switches only once that copy is ready, which
    // lets the browser reuse the already decoded image without a gap.
    loadFullResolution() {
      const sourceWidth = this.image.getAttribute("width");
      if (this.hasFullResolution || !sourceWidth) {
        return;
      }

      this.hasFullResolution = true;
      const sizes = `${sourceWidth}px`;
      const switchToFullResolution = () => {
        for (const candidate of [...this.stage.querySelectorAll(":scope > source[sizes]"), this.image]) {
          if (candidate.hasAttribute("sizes")) {
            candidate.sizes = sizes;
          }
        }
      };

      const preload = this.stage.cloneNode(true);
      const preloadImage = preload.querySelector("img");
      if (!preloadImage || typeof preloadImage.decode !== "function") {
        switchToFullResolution();
        return;
      }

      preloadImage.loading = "eager";
      preloadImage.fetchPriority = "high";
      for (const candidate of [...preload.querySelectorAll(":scope > source[sizes]"), preloadImage]) {
        if (candidate.hasAttribute("sizes")) {
          candidate.sizes = sizes;
        }
      }

      this.preload = preload;
      preloadImage.decode().then(switchToFullResolution, switchToFullResolution).finally(() => {
        this.preload = null;
      });
    }

    getBaseSize() {
      this.baseSize ??= {
        width: this.image.clientWidth,
        height: this.image.clientHeight
      };
      return this.baseSize;
    }

    apply() {
      if (this.isZoomed) {
        this.image.dataset.zoomed = "";
      } else {
        delete this.image.dataset.zoomed;
      }

      this.image.style.setProperty("--zoom-scale", String(this.transform.scale));
      this.image.style.setProperty("--zoom-x", `${this.transform.x}px`);
      this.image.style.setProperty("--zoom-y", `${this.transform.y}px`);
    }

    reset() {
      this.transform = { scale: 1, x: 0, y: 0 };
      this.gesture = null;
      this.pan = null;
      this.baseSize = null;
      delete this.image.dataset.zoomed;
      delete this.image.dataset.dragging;
      this.image.style.removeProperty("--zoom-scale");
      this.image.style.removeProperty("--zoom-x");
      this.image.style.removeProperty("--zoom-y");
    }
  }

  for (const image of targets) {
    new PinchZoom(image);
  }
})();