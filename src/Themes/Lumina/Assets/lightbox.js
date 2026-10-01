// Lightbox dialogs for galleries with photo_viewer = "lightbox".
//
// Opening and closing are plain HTML: thumbnails are <button command="show-modal">
// and the close button is <button command="close"> (Invoker Commands). This
// script adds what HTML cannot do: previous/next between photos (also with the
// arrow keys), focus back on the thumbnail of the photo that was last shown, and
// loading a photo eagerly once its dialog opens.
(() => {
  const dialogs = new Map(
    Array.from(document.querySelectorAll("dialog[data-lightbox]"), (dialog) => [dialog.id, dialog])
  );
  if (dialogs.size === 0) {
    return;
  }

  const hasInvokerCommands = "commandForElement" in HTMLButtonElement.prototype;
  const switching = new WeakSet();

  const thumbnailOf = (dialog) =>
    document.querySelector(`button[command="show-modal"][commandfor="${CSS.escape(dialog.id)}"]`);

  // The dialog images are lazy, so they would only start loading when shown.
  const prioritizeImage = (dialog) => {
    const image = dialog.querySelector("picture > img");
    if (image) {
      image.loading = "eager";
      image.fetchPriority = "high";
    }
  };

  const open = (dialog) => {
    prioritizeImage(dialog);
    dialog.showModal();
  };

  // Previous/next buttons stay hidden without this script.
  for (const button of document.querySelectorAll("[data-lightbox-target]")) {
    button.hidden = false;
  }

  document.addEventListener("click", (event) => {
    const button = event.target.closest("button");
    if (!button) {
      return;
    }

    const target = dialogs.get(button.dataset.lightboxTarget);
    if (target) {
      const current = button.closest("dialog[open]");
      if (current) {
        switching.add(current);
        current.close();
      }

      open(target);
      return;
    }

    // Browsers before Chrome 135, Firefox 144 and Safari 26.2 ignore command/commandfor.
    if (!hasInvokerCommands) {
      const dialog = dialogs.get(button.getAttribute("commandfor"));
      if (dialog && button.getAttribute("command") === "show-modal") {
        open(dialog);
      } else if (dialog && button.getAttribute("command") === "close") {
        dialog.close();
      }
    }
  });

  for (const dialog of dialogs.values()) {
    dialog.addEventListener("command", (event) => {
      if (event.command === "show-modal") {
        prioritizeImage(dialog);
      }
    });

    dialog.addEventListener("keydown", (event) => {
      if (event.defaultPrevented || event.repeat || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey) {
        return;
      }

      const forward = { ArrowRight: true, ArrowLeft: false }[event.key];
      if (forward === undefined) {
        return;
      }

      const rightToLeft = getComputedStyle(dialog).direction === "rtl";
      const direction = forward !== rightToLeft ? "next" : "previous";
      const button = dialog.querySelector(`button[data-photo-${direction}][data-lightbox-target]`);
      if (button) {
        event.preventDefault();
        button.click();
      }
    });

    // The browser returns focus to whatever opened this dialog, which after
    // previous/next is a button inside another, closed dialog.
    dialog.addEventListener("close", () => {
      if (!switching.delete(dialog)) {
        thumbnailOf(dialog)?.focus();
      }
    });
  }
})();
