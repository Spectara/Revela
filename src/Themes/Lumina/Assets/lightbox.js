(() => {
  const dialogs = new Map(
    [...document.querySelectorAll("dialog[data-lightbox]")].map((dialog) => [dialog.id, dialog])
  );

  if (dialogs.size === 0 || typeof HTMLDialogElement === "undefined") {
    return;
  }

  let initiatingTrigger = null;
  let isSwitching = false;

  for (const button of document.querySelectorAll("[data-lightbox-target]")) {
    button.hidden = false;
  }

  const prioritizeImage = (dialog) => {
    const image = dialog.querySelector("picture > img");
    if (image) {
      image.loading = "eager";
      image.fetchPriority = "high";
    }
  };

  const openDialog = (dialog) => {
    prioritizeImage(dialog);
    dialog.showModal();
  };

  const closeDialog = (dialog) => {
    dialog.close();
  };

  const restoreFocus = (dialog) => {
    const trigger = initiatingTrigger ?? document.querySelector(
      `button[command="show-modal"][commandfor="${CSS.escape(dialog.id)}"]`
    );
    initiatingTrigger = null;
    setTimeout(() => trigger?.focus(), 0);
  };

  document.addEventListener("click", (event) => {
    const targetButton = event.target.closest("[data-lightbox-target]");
    if (!targetButton) {
      return;
    }

    const targetDialog = dialogs.get(targetButton.dataset.lightboxTarget);
    if (!targetDialog) {
      return;
    }

    const currentDialog = targetButton.closest("dialog[open]");
    if (currentDialog) {
      isSwitching = true;
      closeDialog(currentDialog);
      queueMicrotask(() => {
        openDialog(targetDialog);
        isSwitching = false;
      });
      return;
    }
  });

  for (const dialog of dialogs.values()) {
    dialog.addEventListener("command", (event) => {
      if (event.command === "show-modal") {
        initiatingTrigger ??= event.source;
        prioritizeImage(dialog);
      } else if (event.command === "close" && !isSwitching) {
        restoreFocus(dialog);
      }
    });

    dialog.addEventListener("close", () => {
      if (isSwitching || initiatingTrigger === null) {
        return;
      }

      restoreFocus(dialog);
    });
  }
})();