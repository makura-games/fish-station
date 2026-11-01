using Robust.Shared.Serialization;

namespace Content.Shared._Fish.CriminalRecords;

/// <summary>
/// Быстрая печать типового документа по уголовному делу.
/// RecordId привязывает запрос к записи человека на консоли.
/// </summary>
[Serializable, NetSerializable]
public sealed class FishCriminalRecordsPrintDocumentMessage : BoundUserInterfaceMessage
{
    public readonly uint RecordId;
    public readonly uint CaseId;
    public readonly string TemplateId;

    public FishCriminalRecordsPrintDocumentMessage(uint recordId, uint caseId, string templateId)
    {
        RecordId = recordId;
        CaseId = caseId;
        TemplateId = templateId;
    }
}

/// <summary>
/// Типы быстрых документов для UI.
/// </summary>
public static class FishQuickDocumentTemplates
{
    public const string Sentence = "FishSentence";
    public const string Warrant = "FishSearchWarrant";
}
