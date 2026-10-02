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
2. Use an explicit pipe sort, otherwise the page `sort`, otherwise `generate.sorting.images`
   (the same sort folder galleries use).
3. Photos without a value for the sort field come last in both directions; for page or global
   sorting they are ordered among themselves by the configured fallback field.
4. Use the filename as a stable final tie-breaker.
5. Apply `limit`.

An explicit pipe sort takes precedence over page and global sorting. `sort random` is drawn
once per render run, for inline blocks and page-level `filter` alike, so the grid, photo pages
and previous/next links share one order and every `generate pages` draws a new one.

## Photo Viewers

The active theme declares which viewer modes it supports and which one is its default:

```json
{
	"photoViewers": ["page", "lightbox", "none"],
	"defaultPhotoViewer": "page"
}
```

Base themes must declare both fields, with a nonempty list of distinct supported modes and a
default contained in that list. Theme extensions cannot declare or override viewer capabilities.
Themes declaring `page` support must provide a resolvable `Body/Photo.revela`; that template is
not required for themes supporting only `lightbox` or `none`.

The project can override the theme default:

```json
{
	"theme": {
		"name": "Lumina",
		"photoViewer": "page"
	}
}
```

A page or gallery can override the project in `_index.revela`:

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

Viewer selection is page-wide, including all inline grids; there is no per-token viewer override.
The mode belongs to an occurrence, not the canonical image. A photo appearing in different
galleries may use different modes. At least one published `page` membership produces one
canonical photo page, containing only the `page` memberships' navigation contexts. Other modes
do not implicitly link to that page.

In `page` mode, the bare token reuses the page's existing photo navigation context. Each filtered
token creates a separate Previous/Back/Next context in its frozen display order. The same photo
may appear in multiple filtered grids; every occurrence receives a distinct HTML anchor and
returns to the correct grid. A Custom Body without a visible inline grid does not publish hidden
images merely because its source directory contains them.

Viewer IDs use collision-free encoding with separate namespaces for the root,
galleries, images and numbered grids. Do not replace this with separator substitution
or truncated hashes: distinct source paths must not share an HTML target. ID encoding
does not change page routes or membership order.

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
- `occurrence.context_label`: the display label for that membership.
- `occurrence.occurrence_id`: the unique identity for this image occurrence.
- `occurrence.previous_occurrence_id`: the previous occurrence, or `null` at the boundary.
- `occurrence.next_occurrence_id`: the next occurrence, or `null` at the boundary.

The gallery body template should suppress its trailing grid when
`gallery.has_inline_galleries` is true. Interactive gallery rendering uses `occurrences`; the
existing `images` model remains available for neutral image data and custom templates. A theme
must not infer the viewer from the body template or from the existence of a photo URL.

Filtered selections are prepared before photo-page aggregation and reused for final rendering.
Theme code must not evaluate the filter again.

## Photo Blocks

`[[photo: <path>]]` places one photo, linked to its photo page, as a standalone top-level block.
The path is resolved like a Markdown content image (page folder, `_images/`, exact source path)
and may contain spaces. Parsing, nesting and escaping follow the gallery token rules; a malformed
token or an unknown option stops generation with the file and line, an unresolved path emits a
warning and renders nothing.

- `[[photo: path]]` adds a single-image page membership (no previous/next) so the photo page's
  return link targets this page at the photo. Photo blocks are numbered in their own namespace
  (context `….photo-n`, anchor `photo-n-photo-i-…`): adding one never renumbers `[[gallery:]]`
  grids (`….grid-n`), whose ids are part of existing links. Ids use the readable slug
  (`/` → `_`, other characters as `~xxxx`), e.g. `#ctx-r.photo-1` or `#photo-i-jahre_2018_002190`.
- `[[photo: path | gallery]]` adds no membership and links without a fragment, so the photo page
  shows its primary context. If the photo has no gallery or grid membership with photo pages
  anywhere, the page context is used instead (with a warning) so the photo page has a way back.
- The link always targets a photo page, independent of the page's viewer mode, and the photo page
  is created even for `_images/`-only photos; it then counts toward pages and the sitemap like
  any other photo page. Photo-block contexts never count as the physical (primary) gallery.
- Photo blocks do not set `gallery.has_inline_galleries`; the trailing grid stays.

Themes render photo blocks with the optional `Partials/PhotoFigure.revela`, which receives
`image`, `viewer_mode`, `context_id`, `context_label`, `occurrence_id`, `basepath`,
`assets_basepath` and `image_formats`. Without it, the `ContentImage` partial is wrapped in the
photo-page link. Themes without photo pages get `viewer_mode = "none"` and no link.

## Lumina Browser Behavior

Lumina's lightbox uses `commandfor` with `command="show-modal"` and `command="close"`. Its
supported browser baseline is Chrome/Edge 135+, Firefox 144+, and Safari/iOS 26.2+. Without
JavaScript, visitors can open and close the modal dialog through its visible controls, inspect the
full-size photo, and read the same image metadata as on the canonical photo page. With JavaScript,
validated Previous/Next controls, arrow-key navigation, and a robust Escape fallback are added.
Conforming browsers also provide native Escape handling. The dialog fills the viewport, so there
is no backdrop to click: it closes with its × button or Escape only (no `closedby="any"` light
dismiss). The layout uses a full-viewport photo
stage followed by a scrolling metadata sheet, without photo-page context navigation.

These capabilities need browser verification; HTML generation alone cannot prove focus, modal
or touch behavior. The retained [browser acceptance requirements](../scripts/browser/README.md)
describe the visitor checks and the current automation gap.