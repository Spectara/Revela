// Arrow keys on photo pages: Left/Right follow the previous/next link of the
// context that is shown, like previous/next in a lightbox dialog. The links
// work without this script; it only adds the keyboard shortcut.
(() => {
  const nav = document.querySelector("body.photo-page nav.photo-nav");
  if (!nav) {
    return;
  }

  // Same rule as photo.css: the context named by the fragment, else the primary one.
  const shownContext = () => {
    let id = "";
    try {
      id = decodeURIComponent(location.hash.slice(1));
    } catch {
      // A malformed fragment matches no context.
    }

    const target = id ? document.getElementById(id) : null;
    return target?.parentElement === nav ? target : nav.querySelector(":scope > [data-primary]");
  };

  const isEditable = (element) =>
    element instanceof HTMLElement &&
    (element.isContentEditable || element.closest("input, textarea, select") !== null);

  document.addEventListener("keydown", (event) => {
    if (event.defaultPrevented || event.repeat || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) {
      return;
    }

    const forward = { ArrowRight: true, ArrowLeft: false }[event.key];
    if (forward === undefined || isEditable(event.target)) {
      return;
    }

    const rightToLeft = getComputedStyle(nav).direction === "rtl";
    const direction = forward !== rightToLeft ? "next" : "previous";
    const link = shownContext()?.querySelector(`a[data-photo-${direction}]`);
    if (link) {
      event.preventDefault();
      link.click();
    }
  });
})();
