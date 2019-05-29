using Autodesk.Maya.OpenMaya;
using Autodesk.Maya.OpenMayaAnim;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace CODTools
{
    internal class CODXAnim
    {
        public enum XAnimType
        {
            Export,
            Bin,
            SiegeAnimSource
        }

        public static void ExportXAnim(string FilePath, XAnimType FileType, bool Grab = true, bool Edit = false)
        {
            // Configure scene
            using (var MayaCfg = new MayaSceneConfigure())
            {
                // First, get the current selection
                var ExportObjectList = new MSelectionList();
                MGlobal.getActiveSelectionList(ExportObjectList);

                // If empty, select all joints
                if (ExportObjectList.DependNodes(MFn.Type.kJoint).Count() == 0)
                {
                    // Select all joints
                    MGlobal.executeCommand("string $selected[] = `ls -type joint`; select -r $selected;");
                    // Get it again
                    MGlobal.getActiveSelectionList(ExportObjectList);
                }

                // If still empty, error blank scene
                if (ExportObjectList.DependNodes(MFn.Type.kJoint).Count() == 0)
                {
                    MGlobal.displayError("[CODTools] The current scene has no joints...");
                    return;
                }

                // Progress
                MayaCfg.StartProgress("Exporting XAnim...", ((int)ExportObjectList.length + Math.Max((MayaCfg.SceneEnd - MayaCfg.SceneStart) + 1, 1)));

                // Create new anim
                var Result = new XAnim(System.IO.Path.GetFileNameWithoutExtension(FilePath));

                // Metadata
                var SceneName = string.Empty;
                MGlobal.executeCommand("file -q -sceneName", out SceneName);

                Result.Comments.Add(string.Format("Export filename: '{0}'", FilePath));
                Result.Comments.Add(string.Format("Source filename: '{0}'", SceneName));
                Result.Comments.Add(string.Format("Export time: {0}", DateTime.Now.ToString()));

                // Iterate and add joints
                var UniqueBones = new HashSet<string>();
                var JointControllers = new List<MFnIkJoint>();

                foreach (var Joint in ExportObjectList.DependNodes(MFn.Type.kJoint))
                {
                    // Step
                    MayaCfg.StepProgress();

                    // Grab the controller
                    var Path = CODXModel.GetObjectDagPath(Joint);
                    var Controller = new MFnIkJoint(Path);

                    // Create a new bone
                    var TagName = CODXModel.CleanNodeName(Controller.name);

                    if (UniqueBones.Contains(TagName))
                        continue;
                    UniqueBones.Add(TagName);

                    // Add to the controller list
                    JointControllers.Add(Controller);

                    // Add to the part list
                    Result.Parts.Add(new Part(TagName));
                }

                // Iterate over the frame range, then generate part frames
                for (int i = MayaCfg.SceneStart; i < (MayaCfg.SceneEnd + 1); i++)
                {
                    // Step and set time
                    MayaCfg.StepProgress();
                    MayaCfg.SetTime(i);

                    // Iterate over the parts for this time
                    for (int p = 0; p < JointControllers.Count; p++)
                    {
                        // Make new frame
                        var NewFrame = new PartFrame();

                        // Fetch the world-space position and rotation
                        var WorldPosition = JointControllers[p].getTranslation(MSpace.Space.kWorld);

                        var WorldRotation = new MQuaternion(MQuaternion.identity);
                        JointControllers[p].getRotation(WorldRotation, MSpace.Space.kWorld);

                        // Create the matrix
                        NewFrame.Offset = WorldPosition * (1 / 2.54);
                        NewFrame.RotationMatrix = WorldRotation.asMatrix;

                        // Add it
                        Result.Parts[p].Frames.Add(NewFrame);
                    }
                }

                // Reset time
                MayaCfg.SetTime(MayaCfg.SceneStart);

                // Grab XAnim notetracks
                if (Grab)
                    LoadNotetracks(ref Result);

                // Write
                switch (FileType)
                {
                    case XAnimType.Export:
                        Result.WriteExport(FilePath);
                        break;
                    case XAnimType.Bin:
                        Result.WriteBin(FilePath);
                        break;
                    case XAnimType.SiegeAnimSource:
                        Result.WriteSiegeSource(FilePath);
                        break;
                }
            }

            // Log complete
            MGlobal.displayInfo(string.Format("[CODTools] Exported {0}", System.IO.Path.GetFileName(FilePath)));
        }

        private static void LoadNotetracks(ref XAnim Anim)
        {
            try
            {
                var SelectList = new MSelectionList();
                SelectList.add("SENotes");

                if (SelectList.length == 0)
                    return;

                // Get path
                var NotePath = new MDagPath();
                SelectList.getDagPath(0, NotePath);

                // Get node
                var Dep = new MFnDependencyNode(NotePath.node);
                var NotePlug = Dep.findPlug("Notetracks");

                var ResultJson = "{}";
                NotePlug.getValue(out ResultJson);

                // Deserialize
                var ResultNotes = new JavaScriptSerializer().Deserialize<Dictionary<string, List<int>>>(ResultJson);

                // Append
                foreach (var Note in ResultNotes)
                {
                    foreach (var Frame in Note.Value)
                        Anim.Notetracks.Add(new Notetrack(Note.Key, Frame));
                }
            }
            catch
            {
                // Nothing..
            }
        }
    }
}
