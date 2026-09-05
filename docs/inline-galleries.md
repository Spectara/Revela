# Inline Galleries

Inline galleries place a photo grid at a specific position in an `_index.revela` Markdown body.
They use the same filter language and effective image sorting as gallery frontmatter.

## Syntax

Use a standalone top-level block:

```text
Text before the photos.

[[gallery]]

Text after the photos.
```

The bare form renders the page's effective image set. A filtered form selects from all canonical
manifest images, including shared images under `_images`:

```text
[[gallery: width > height | limit 6]]

[[gallery: contains(sourcePath, 'travel/') | sort dateTaken desc | limit 12]]
```

The block must be on its own top-level line. Tokens in lists, blockquotes, code blocks, inline
code, or surrounding paragraph text remain literal and produce a warning. Escape an intentional
literal with `\[[gallery]]`.

Malformed standalone tokens and invalid filters stop generation with the `_index.revela` path,
line, filter expression, and error position. A valid filter with no matches emits a warning and
no empty gallery section.

## Ordering

Image queries always run in this order:

1. Apply the filter predicate.
2. Use an explicit pipe sort, otherwise the page `sort`, otherwise `generate.sorting.images`.
3. Apply the configured fallback field for page or global sorting.
4. Use the filename as a stable final tie-breaker.
5. Apply `limit`.

An explicit pipe sort takes precedence over page and global sorting.

## Photo Viewers

The active theme declares which viewer modes it supports and which one is its default:

```json
{
	"photoViewers": ["page", "lightbox", "none"],
	"defaultPhotoViewer": "page"
}
```

The project can override that default with `theme.photoViewer`. A page or gallery can override
the project in `_index.revela`:

```text
+++
photo_viewer = "lightbox"
+++
```

The effective order is page or gallery, project, then theme default. Supported modes are:

- `page`: open a generated canonical photo page.
- `lightbox`: use the theme's in-page viewer without generating a photo page for that occurrence.
	Lumina opens and closes its modal dialog declaratively through HTML Invoker Commands; JavaScript
	only adds Previous/Next navigation and arrow keys.
- `none`: render a non-interactive image.

The body template does not change this behavior. Custom `home` and `page` templates use the same
rules as default gallery bodies. Unsupported modes fail generation with the theme's supported
values instead of silently falling back.

In `page` mode, the bare token reuses the page's existing photo navigation context. Each filtered
token creates a separate Previous/Back/Next context in its frozen display order. The same photo
may appear in multiple filtered grids; every occurrence receives a distinct HTML anchor and
returns to the correct grid. A Custom Body without a visible inline grid does not publish hidden
images merely because its source directory contains them.

Once a valid inline token is present, Lumina suppresses the automatic trailing gallery grid. This
also applies when the token matches no photos. Multiple bare tokens are allowed, but generate a
warning because they repeat the same page image set.

## Theme Contract

Themes that support inline galleries provide `Partials/GalleryGrid.revela`. The partial is loaded
only when a page uses an inline token, so themes without it continue to work for pages without
inline galleries. The grid receives prepared image occurrences with:

- `occurrence.image`: the normal image template model.
- `occurrence.viewer_mode`: lowercase `page`, `lightbox`, or `none`.
- `occurrence.context_id`: the stable membership identity.
- `occurrence.occurrence_id`: the unique identity for this image occurrence.
- `occurrence.previous_occurrence_id`: the previous occurrence, or `null` at the boundary.
- `occurrence.next_occurrence_id`: the next occurrence, or `null` at the boundary.

The gallery body template should suppress its trailing grid when
`gallery.has_inline_galleries` is true. Interactive gallery rendering uses `occurrences`; the
existing `images` model remains available for neutral image data and custom templates. A theme
must not infer the viewer from the body template or from the existence of a photo URL.

Filtered selections are prepared before photo-page aggregation and reused for final rendering.
Theme code must not evaluate the filter again.

## Photo Viewer Architecture

> **Status:** Implemented. This contract supersedes the former D6 template-based rule.

The concrete implementation sequence, file touchpoints, test matrix, and acceptance criteria are
tracked in
[`docs/ideas/photo-viewer-implementation-plan.md`](ideas/photo-viewer-implementation-plan.md).

The photo viewer must not be inferred from a page's body template. A `[[gallery]]` block should
behave predictably whether its page uses the default gallery body, `home`, `page`, or a custom
theme template.

Revela provides the publication and navigation mechanisms. Themes choose which presentation
modes they implement and declare those capabilities in their manifest:

```json
{
	"photoViewers": ["page", "lightbox", "none"],
	"defaultPhotoViewer": "page"
}
```

The modes are:

- `page`: open a generated canonical photo page with the prepared navigation context.
- `lightbox`: open a theme-provided inline lightbox without generating a photo page for that
	occurrence.
- `none`: render a non-interactive image occurrence.

The project may override the theme default explicitly:

```json
{
	"theme": {
		"name": "Lumina",
		"photoViewer": "page"
	}
}
```

A gallery or page may override the project choice in `_index.revela`:

```text
+++
photo_viewer = "lightbox"
+++
```

The effective mode resolves in this order:

1. Page or gallery `photo_viewer`.
2. Project `theme.photoViewer`.
3. Theme manifest `defaultPhotoViewer`.

Revela validates the effective mode against the active theme's `photoViewers`. Unsupported modes
must fail generation with the theme name and its supported modes; Revela must not silently fall
back to another interaction.

The mode belongs to the image occurrence, not to the canonical image identity. The same image may
open a photo page in one gallery, use a lightbox on another page, and remain static elsewhere. A
canonical photo page is generated when at least one published membership uses `page`.

The Core remains responsible for effective configuration, prepared memberships, stable ordering,
navigation contexts, and photo-page generation. The theme remains responsible for page-link,
lightbox, and static-image markup and styling. Themes may support any subset of the modes; Revela
does not require every theme to implement every presentation.

Viewer selection is intentionally page-wide. Per-token viewer overrides are not planned because
mixing interaction models within one page would be difficult for visitors to predict and would
unnecessarily expand the inline-gallery syntax.

Lumina's lightbox uses `commandfor` with `command="show-modal"` and `command="close"`. Its
supported browser baseline is Chrome/Edge 135+, Firefox 144+, and Safari/iOS 26.2+. Without
JavaScript, visitors can open and close the modal dialog through its visible controls, inspect the
full-size photo, and read the same image metadata as on the canonical photo page. With JavaScript,
validated Previous/Next controls, arrow-key navigation, and a robust Escape fallback are added.
Conforming browsers also provide native Escape handling. The image-first layout follows the
prototype direction: a full-viewport sticky photo stage followed by a translucent scrolling
metadata sheet, without photo-page context navigation.