using Autodesk.Maya.OpenMaya;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CODTools
{
    public class CODXAnimExport : MPxFileTranslator
    {
        public override string defaultExtension()
        {
            // Default extension
            return "XANIM_EXPORT";
        }

        public override bool haveReadMethod()
        {
            return false;
        }

        public override bool haveWriteMethod()
        {
            return true;
        }

        public override MPxFileTranslator.MFileKind identifyFile(MFileObject file, string buffer, short bufferLen)
        {
            // It's our file
            if (file.fullName.ToUpper().EndsWith(".XANIM_EXPORT"))
                return MFileKind.kIsMyFileType;

            // Failed
            return MFileKind.kNotMyFileType;
        }

        public override void writer(MFileObject file, string optionsString, MPxFileTranslator.FileAccessMode mode)
        {
            // Prepare to export, pass it off
            if (file.fullName.ToUpper().EndsWith(".XANIM_EXPORT"))
            {
                // Parse settings
                bool GrabNotes = true, EditNotes = false;

                var SplitSettings = optionsString.Trim().Split(';');
                foreach (var Setting in SplitSettings)
                {
                    if (string.IsNullOrWhiteSpace(Setting))
                        continue;

                    var SettingValue = Setting.Split('=');
                    if (SettingValue.Length < 2)
                        continue;

                    if (SettingValue[0] == "grabnotes")
                        GrabNotes = (SettingValue[1] == "1");
                    else if (SettingValue[0] == "editnotes")
                        EditNotes = (SettingValue[1] == "1");
                }

                // Export anim
                CODXAnim.ExportXAnim(file.fullName, CODXAnim.XAnimType.Export, GrabNotes, EditNotes);
            }
        }

        public override void reader(MFileObject file, string optionsString, MPxFileTranslator.FileAccessMode mode)
        {
            throw new NotImplementedException("We only support support writer not reader");
        }
    }

    public class CODXAnimBin : MPxFileTranslator
    {
        public override string defaultExtension()
        {
            // Default extension
            return "XANIM_BIN";
        }

        public override bool haveReadMethod()
        {
            return false;
        }

        public override bool haveWriteMethod()
        {
            return true;
        }

        public override MPxFileTranslator.MFileKind identifyFile(MFileObject file, string buffer, short bufferLen)
        {
            // It's our file
            if (file.fullName.ToUpper().EndsWith(".XANIM_BIN"))
                return MFileKind.kIsMyFileType;

            // Failed
            return MFileKind.kNotMyFileType;
        }

        public override void writer(MFileObject file, string optionsString, MPxFileTranslator.FileAccessMode mode)
        {
            // Prepare to export, pass it off
            if (file.fullName.ToUpper().EndsWith(".XANIM_BIN"))
            {
                // Parse settings
                bool GrabNotes = true, EditNotes = false;

                var SplitSettings = optionsString.Trim().Split(';');
                foreach (var Setting in SplitSettings)
                {
                    if (string.IsNullOrWhiteSpace(Setting))
                        continue;

                    var SettingValue = Setting.Split('=');
                    if (SettingValue.Length < 2)
                        continue;

                    if (SettingValue[0] == "grabnotes")
                        GrabNotes = (SettingValue[1] == "1");
                    else if (SettingValue[0] == "editnotes")
                        EditNotes = (SettingValue[1] == "1");
                }

                // Export anim
                CODXAnim.ExportXAnim(file.fullName, CODXAnim.XAnimType.Bin, GrabNotes, EditNotes);
            }
        }

        public override void reader(MFileObject file, string optionsString, MPxFileTranslator.FileAccessMode mode)
        {
            throw new NotImplementedException("We only support support writer not reader");
        }
    }

    public class CODXAnimSiege : MPxFileTranslator
    {
        public override string defaultExtension()
        {
            // Default extension
            return "SIEGE_ANIM_SOURCE";
        }

        public override bool haveReadMethod()
        {
            return false;
        }

        public override bool haveWriteMethod()
        {
            return true;
        }

        public override MPxFileTranslator.MFileKind identifyFile(MFileObject file, string buffer, short bufferLen)
        {
            // It's our file
            if (file.fullName.ToUpper().EndsWith(".SIEGE_ANIM_SOURCE"))
                return MFileKind.kIsMyFileType;

            // Failed
            return MFileKind.kNotMyFileType;
        }

        public override void writer(MFileObject file, string optionsString, MPxFileTranslator.FileAccessMode mode)
        {
            // Prepare to export, pass it off
            if (file.fullName.ToUpper().EndsWith(".SIEGE_ANIM_SOURCE"))
                CODXAnim.ExportXAnim(file.fullName, CODXAnim.XAnimType.SiegeAnimSource);
        }

        public override void reader(MFileObject file, string optionsString, MPxFileTranslator.FileAccessMode mode)
        {
            throw new NotImplementedException("We only support support writer not reader");
        }
    }
}
