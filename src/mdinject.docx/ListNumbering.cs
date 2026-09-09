using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Mdinject.Docx;

/// <summary>
/// Registers a single-level bullet or decimal numbering definition in the output document's
/// numbering part, per markdown list occurrence - never reused across separate list blocks, so
/// each one always starts fresh at "1." rather than continuing a previous list's count. This is
/// direct formatting mdinject owns outright (like bold/italic), not a template style: it works
/// whether or not the template defines its own numbering.
/// </summary>
internal static class ListNumbering
{
    private const string BulletLevelText = "•";

    /// <summary>
    /// Adds a fresh <c>w:abstractNum</c>/<c>w:num</c> pair (<see cref="AbstractNum"/> /
    /// <see cref="NumberingInstance"/>) to the output document's numbering part and returns the new
    /// numId to reference from a paragraph's <see cref="NumberingId"/>.
    /// </summary>
    public static int EnsureListDefinition(MainDocumentPart mainPart, bool ordered)
    {
        var numberingPart = mainPart.NumberingDefinitionsPart ?? mainPart.AddNewPart<NumberingDefinitionsPart>();
        numberingPart.Numbering ??= new Numbering();

        var numbering = numberingPart.Numbering;

        var nextAbstractNumId = numbering.Elements<AbstractNum>()
            .Select(a => a.AbstractNumberId?.Value ?? 0)
            .DefaultIfEmpty(-1)
            .Max() + 1;

        var nextNumId = numbering.Elements<NumberingInstance>()
            .Select(n => n.NumberID?.Value ?? 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        var abstractNum = BuildAbstractNum(nextAbstractNumId, ordered);
        var existingAbstractNums = numbering.Elements<AbstractNum>().ToList();
        if (existingAbstractNums.Count > 0)
            numbering.InsertAfter(abstractNum, existingAbstractNums[^1]);
        else
            numbering.InsertAt(abstractNum, 0);

        numbering.Append(new NumberingInstance(new AbstractNumId { Val = nextAbstractNumId }) { NumberID = nextNumId });

        return nextNumId;
    }

    private static AbstractNum BuildAbstractNum(int abstractNumId, bool ordered)
    {
        var level = new Level
        {
            LevelIndex = 0,
            StartNumberingValue = new StartNumberingValue { Val = 1 },
            NumberingFormat = new NumberingFormat { Val = ordered ? NumberFormatValues.Decimal : NumberFormatValues.Bullet },
            LevelText = new LevelText { Val = ordered ? "%1." : BulletLevelText },
            LevelJustification = new LevelJustification { Val = LevelJustificationValues.Left },
            PreviousParagraphProperties = new PreviousParagraphProperties(new Indentation { Left = "720", Hanging = "360" }),
        };

        return new AbstractNum(level) { AbstractNumberId = abstractNumId };
    }
}
