using CohEngine.Core.Chain;

namespace CohEngine.Core.BuildFile;

/// <summary>The value tree <see cref="Mbd.Serialize"/> hands to <see cref="PyJson"/>: keys in file order.</summary>
public static partial class Mbd
{
    private static PyObject ToTree(MbdDocument document)
    {
        return new PyObject
        {
            { "BuiltWith", document.BuiltWith is { } meta ? new PyObject { { "App", meta.App }, { "Version", meta.Version }, { "Database", meta.Database }, { "DatabaseVersion", meta.DatabaseVersion } } : null },
            { "Level", document.Level },
            { "Class", document.Class },
            { "Origin", document.Origin },
            { "Alignment", document.Alignment },
            { "Name", document.Name },
            { "Comment", document.Comment },
            { "PowerSets", document.PowerSets },
            { "LastPower", document.LastPower },
            { "PowerEntries", document.PowerEntries.Select(EntryTree).ToList() },
        };
    }

    private static PyObject EntryTree(MbdPowerEntryDoc entry)
    {
        return new PyObject
        {
            { "PowerName", entry.PowerName },
            { "Level", entry.Level },
            { "StatInclude", entry.StatInclude },
            { "ProcInclude", entry.ProcInclude },
            { "VariableValue", entry.VariableValue },
            { "InherentSlotsUsed", entry.InherentSlotsUsed },
            { "SubPowerEntries", entry.SubPowerEntries.Select(sub => new PyObject { { "PowerName", sub.PowerName }, { "StatInclude", sub.StatInclude } }).ToList() },
            { "SlotEntries", entry.SlotEntries.Select(slot => new PyObject { { "Level", slot.Level }, { "IsInherent", slot.IsInherent }, { "Enhancement", EnhancementTree(slot.Enhancement) }, { "FlippedEnhancement", EnhancementTree(slot.FlippedEnhancement) } }).ToList() },
        };
    }

    // A null Uid or legacy Enhancement key is left out, as the writer has always done.
    private static PyObject? EnhancementTree(MbdEnhancementDoc? enhancement)
    {
        if (enhancement is null)
        {
            return null;
        }

        PyObject tree = [];
        if (enhancement.Uid is not null)
        {
            tree.Add("Uid", enhancement.Uid);
        }

        tree.Add("Grade", enhancement.Grade);
        tree.Add("IoLevel", enhancement.IoLevel);
        tree.Add("RelativeLevel", enhancement.RelativeLevel);
        tree.Add("Obtained", enhancement.Obtained);
        if (enhancement.Enhancement is not null)
        {
            tree.Add("Enhancement", enhancement.Enhancement);
        }

        return tree;
    }
}
