using DocumentFormat.OpenXml.Packaging;
using Mdinject.Core;
using Mdinject.Core.DocumentModel;
using Wps = DocumentFormat.OpenXml.Wordprocessing;
using Dwg = DocumentFormat.OpenXml.Drawing;
using DwgWp = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using Pic = DocumentFormat.OpenXml.Drawing.Pictures;

namespace Mdinject.Docx;

/// <summary>
/// Embeds a standalone image as an inline <c>w:drawing</c> run: reads the file from disk (resolving
/// a relative <see cref="DocumentBlock.Image.Source"/> against a base directory), adds it to the
/// document's image parts, and sizes it to its natural pixel dimensions - no resizing, cropping, or
/// format conversion. PNG and JPEG are the only formats supported so far.
/// </summary>
internal static class ImageEmbedder
{
    // 96 DPI (the assumption for on-screen/web images, which is what a markdown-authored image
    // almost always is) at 914400 EMU per inch.
    private const double EmuPerPixel = 914400.0 / 96.0;

    public static Wps.Run BuildImageRun(MainDocumentPart mainPart, string basePath, DocumentBlock.Image image)
    {
        var resolvedPath = ResolvePath(basePath, image.Source);
        if (!File.Exists(resolvedPath))
            throw new ImageProcessingException($"Image file not found: '{resolvedPath}'.");

        var bytes = File.ReadAllBytes(resolvedPath);
        var (partType, (widthPx, heightPx)) = ReadFormatAndDimensions(resolvedPath, bytes);

        var imagePart = mainPart.AddImagePart(partType);
        using (var stream = new MemoryStream(bytes))
            imagePart.FeedData(stream);

        var relationshipId = mainPart.GetIdOfPart(imagePart);
        var name = Path.GetFileName(resolvedPath);
        var widthEmu = (long)(widthPx * EmuPerPixel);
        var heightEmu = (long)(heightPx * EmuPerPixel);

        var picture = new Pic.Picture(
            new Pic.NonVisualPictureProperties(
                new Pic.NonVisualDrawingProperties { Id = 0, Name = name, Description = image.AltText },
                new Pic.NonVisualPictureDrawingProperties()),
            new Pic.BlipFill(
                new Dwg.Blip { Embed = relationshipId },
                new Dwg.Stretch(new Dwg.FillRectangle())),
            new Pic.ShapeProperties(
                new Dwg.Transform2D(
                    new Dwg.Offset { X = 0, Y = 0 },
                    new Dwg.Extents { Cx = widthEmu, Cy = heightEmu }),
                new Dwg.PresetGeometry(new Dwg.AdjustValueList()) { Preset = Dwg.ShapeTypeValues.Rectangle }));

        var graphic = new Dwg.Graphic(
            new Dwg.GraphicData(picture) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" });

        var inline = new DwgWp.Inline(
            new DwgWp.Extent { Cx = widthEmu, Cy = heightEmu },
            new DwgWp.DocProperties { Id = 0, Name = name, Description = image.AltText },
            graphic);

        return new Wps.Run(new Wps.Drawing(inline));
    }

    private static string ResolvePath(string basePath, string source)
    {
        return Path.IsPathRooted(source) ? source : Path.Combine(basePath, source);
    }

    private static (PartTypeInfo PartType, (int Width, int Height)) ReadFormatAndDimensions(string path, byte[] bytes)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();

        return extension switch
        {
            ".png" => (ImagePartType.Png, ReadPngDimensions(bytes)),
            ".jpg" or ".jpeg" => (ImagePartType.Jpeg, ReadJpegDimensions(bytes)),
            _ => throw new ImageProcessingException($"Unsupported image format '{extension}' for '{path}'. Only PNG and JPEG are supported so far."),
        };
    }

    private static (int Width, int Height) ReadPngDimensions(byte[] bytes)
    {
        // PNG signature (8 bytes) is immediately followed by the IHDR chunk: 4-byte length, 4-byte
        // "IHDR", then big-endian width (4 bytes) and height (4 bytes).
        if (bytes.Length < 24)
            throw new ImageProcessingException("Not a valid PNG file: too short to contain an IHDR chunk.");

        var width = ReadBigEndianInt32(bytes, 16);
        var height = ReadBigEndianInt32(bytes, 20);
        return (width, height);
    }

    private static (int Width, int Height) ReadJpegDimensions(byte[] bytes)
    {
        var offset = 2; // Skip the SOI marker (0xFFD8).

        while (offset < bytes.Length - 1)
        {
            if (bytes[offset] != 0xFF)
                throw new ImageProcessingException("Not a valid JPEG file: expected a marker.");

            var marker = bytes[offset + 1];
            offset += 2;

            // SOI/EOI and the RSTn markers carry no length-prefixed segment to skip.
            if (marker == 0xD8 || marker == 0xD9 || (marker >= 0xD0 && marker <= 0xD7))
                continue;

            var segmentLength = (bytes[offset] << 8) | bytes[offset + 1];

            // SOFn markers (0xC0-0xCF, excluding DHT/JPG/DAC at 0xC4/0xC8/0xCC) carry the frame's
            // dimensions: 1-byte precision, then big-endian height and width.
            var isStartOfFrame = marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC;
            if (isStartOfFrame)
            {
                var height = (bytes[offset + 3] << 8) | bytes[offset + 4];
                var width = (bytes[offset + 5] << 8) | bytes[offset + 6];
                return (width, height);
            }

            offset += segmentLength;
        }

        throw new ImageProcessingException("Could not determine JPEG image dimensions: no SOF marker found.");
    }

    private static int ReadBigEndianInt32(byte[] bytes, int offset)
    {
        return (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
    }
}
