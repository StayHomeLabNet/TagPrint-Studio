# Layout Specification

TagPrint Studio stores each layout as a `.tpslayout` JSON file.

## Version

- `formatVersion` is currently `1`.
- Increase `formatVersion` only when a change breaks compatibility.
- Additive fields that can use default values should stay compatible with version `1`.

## Coordinate System

- `paper`, `tag`, `margin`, `spacing`, and `elements` are currently treated as millimeters.
- The origin is the top-left corner.
- `LayoutElement.X` and `LayoutElement.Y` are relative to the top-left corner of a tag.
- `LayoutElement.Width` and `LayoutElement.Height` are the element size inside a tag.
- The Designer should treat elements outside the tag bounds as validation errors.

## Paper And Grid

- `PaperSettings.CustomWidth` and `CustomHeight` are used only when `PaperSize` is `Custom`.
- When `Grid.AutoCalculate=true`, columns and rows are calculated from paper size, margins, tag size, and spacing.
- When `Grid.AutoCalculate=false`, `Columns` and `Rows` must be greater than zero.

## Elements

- `FieldText` renders a CSV field selected by `FieldId`.
- `StaticText` renders the literal `Text` value.
- `Image` renders the file at `ImagePath`.
- Elements with `Visible=false` are skipped by preview and export.
- Elements are drawn by ascending `ZIndex`; larger values appear in front.
- Designers should normally assign unique `ZIndex` values, although old files may contain duplicates.

## Validation

Before saving a layout, validate at least:

- Layout name is not empty.
- Tag width and height are greater than zero.
- Margins and spacing are zero or greater.
- Manual grid columns and rows are greater than zero.
- Every element has an `ElementId`.
- Every element has width and height greater than zero.
- Every element has non-negative `X` and `Y`.
- Every element stays inside the tag bounds.

## Designer MVP

The first Layout Designer should be property-based before adding drag editing.

- Load a layout file.
- Show the element list.
- Add, delete, show, and hide elements.
- Edit `X`, `Y`, `Width`, `Height`, `ZIndex`, `FontSize`, `Bold`, `TextAlign`, `Text`, and `FieldId`.
- Refresh preview.
- Validate and save.
