using Autodesk.Maya.OpenMaya;
using Autodesk.Maya.OpenMayaAnim;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CODTools
{
    internal class CODXModel
    {
        public enum XModelType
        {
            Export,
            Bin
        }

        public static void ExportXModel(string FilePath, XModelType FileType, bool Siege = false, string Cosmetic="")
        {
            // Configure scene
            using (var MayaCfg = new MayaSceneConfigure())
            {
                // First, get the current selection
                var ExportObjectList = new MSelectionList();
                MGlobal.getActiveSelectionList(ExportObjectList);

                // If empty, select all joints and meshes
                if (ExportObjectList.length == 0)
                {
                    // Select all joints and meshes
                    MGlobal.executeCommand("string $selected[] = `ls -type joint`; select -r $selected;");
                    MGlobal.executeCommand("string $transforms[] = `ls -tr`;string $polyMeshes[] = `filterExpand -sm 12 $transforms`;select -add $polyMeshes;");

                    // Get it again
                    MGlobal.getActiveSelectionList(ExportObjectList);
                }

                // If still empty, error blank scene
                if (ExportObjectList.length == 0)
                {
                    MGlobal.displayError("[CODTools] The current scene is empty...");
                    return;
                }

                // Progress
                MayaCfg.StartProgress("Exporting XModel...", (int)ExportObjectList.length);

                // Create new model
                var Result = new XModel(System.IO.Path.GetFileNameWithoutExtension(FilePath));
                // Assign siege model flag (Default: false)
                Result.SiegeModel = Siege;

                // Metadata
                var SceneName = string.Empty;
                MGlobal.executeCommand("file -q -sceneName", out SceneName);

                Result.Comments.Add(string.Format("Export filename: '{0}'", FilePath));
                Result.Comments.Add(string.Format("Source filename: '{0}'", SceneName));
                Result.Comments.Add(string.Format("Export time: {0}", DateTime.Now.ToString()));

                // Iterate and add joints
                var ParentStack = new List<string>();
                var UniqueBones = new HashSet<string>();

                foreach (var Joint in ExportObjectList.DependNodes(MFn.Type.kJoint))
                {
                    // Step
                    MayaCfg.StepProgress();

                    // Grab the controller
                    var Path = GetObjectDagPath(Joint);
                    var Controller = new MFnIkJoint(Path);

                    // Create a new bone
                    var TagName = CleanNodeName(Controller.name);

                    if (UniqueBones.Contains(TagName))
                        continue;
                    UniqueBones.Add(TagName);

                    var NewBone = new Bone(TagName);
                    // Add parent
                    ParentStack.Add(GetParentName(Controller));

                    // Fetch the world-space position and rotation
                    var WorldPosition = Controller.getTranslation(MSpace.Space.kWorld);

                    var WorldRotation = new MQuaternion(MQuaternion.identity);
                    Controller.getRotation(WorldRotation, MSpace.Space.kWorld);

                    var WorldScale = new double[3] { 1, 1, 1 };
                    Controller.getScale(WorldScale);

                    // Create the matrix
                    NewBone.Translation = WorldPosition * (1 / 2.54);
                    NewBone.Scale = new MVector(WorldScale[0], WorldScale[1], WorldScale[2]);
                    NewBone.RotationMatrix = WorldRotation.asMatrix;

                    // Add it
                    Result.Bones.Add(NewBone);
                }
                // Sort joints
                SortJoints(ref Result, ParentStack, Cosmetic);

                // Pre-fetch skins
                var SkinClusters = GetSkinClusters();
                var BoneMapping = Result.GetBoneMapping();

                // A list of used materials
                int MaterialIndex = 0;
                var UsedMaterials = new Dictionary<string, int>();
                var UsedMeshes = new HashSet<string>();

                // Iterate and add meshes
                foreach (var Mesh in ExportObjectList.DependNodes(MFn.Type.kMesh))
                {
                    // Step
                    MayaCfg.StepProgress();

                    // Grab the controller
                    var Path = GetObjectDagPath(Mesh);
                    Path.extendToShape();
                    var Controller = new MFnMesh(Path);

                    // Ignore duplicates
                    if (UsedMeshes.Contains(Path.partialPathName))
                        continue;
                    UsedMeshes.Add(Path.partialPathName);

                    // Pre-fetch materials
                    var MeshMaterials = GetMaterialsMesh(ref Controller, ref Path);
                    foreach (var Mat in MeshMaterials)
                    {
                        if (!UsedMaterials.ContainsKey(Mat.Name))
                        {
                            UsedMaterials.Add(Mat.Name, MaterialIndex++);
                            Result.Materials.Add(Mat);
                        }
                    }

                    // New mesh
                    var NewMesh = new Mesh();

                    // Grab iterators
                    var VertexIterator = new MItMeshVertex(Path);
                    var FaceIterator = new MItMeshPolygon(Path);

                    // Get the cluster for this
                    var SkinCluster = FindSkinCluster(ref SkinClusters, Controller);
                    var SkinJoints = new MDagPathArray();

                    if (SkinCluster != null)
                        SkinCluster.influenceObjects(SkinJoints);

                    // Build vertex array
                    for (; !VertexIterator.isDone; VertexIterator.next())
                    {
                        // Prepare
                        var NewVert = new Vertex();

                        // Grab data
                        NewVert.Position = VertexIterator.position(MSpace.Space.kWorld) * (1 / 2.54);

                        // Weights if valid
                        if (SkinCluster != null)
                        {
                            var WeightValues = new MDoubleArray();

                            uint Influence = 0;
                            SkinCluster.getWeights(Path, VertexIterator.currentItem(), WeightValues, ref Influence);

                            for (int i = 0; i < (int)WeightValues.length; i++)
                            {
                                if (WeightValues[i] < 0.000001)
                                    continue;
                                var WeightTagName = CleanNodeName(SkinJoints[i].partialPathName);
                                var WeightID = (BoneMapping.ContainsKey(WeightTagName)) ? BoneMapping[WeightTagName] : 0;

                                NewVert.Weights.Add(new Tuple<int, float>(WeightID, (float)WeightValues[i]));
                            }
                        }
                        if (NewVert.Weights.Count == 0)
                            NewVert.Weights.Add(new Tuple<int, float>(0, 1.0f));

                        // Add it
                        NewMesh.Vertices.Add(NewVert);
                    }

                    // Build face array
                    for (; !FaceIterator.isDone; FaceIterator.next())
                    {
                        var Indices = new MIntArray();
                        var Normals = new MVectorArray();
                        var UVUs = new MFloatArray();
                        var UVVs = new MFloatArray();

                        FaceIterator.getVertices(Indices);
                        FaceIterator.getNormals(Normals, MSpace.Space.kWorld);
                        FaceIterator.getUVs(UVUs, UVVs);

                        // Only support TRIS/QUAD
                        if (Indices.Count < 3)
                            continue;

                        if (Indices.Count == 3)
                        {
                            // Create new face
                            var NewFace = new FaceVertex();
                            // Setup
                            NewFace.Indices[0] = Indices[0];
                            NewFace.Indices[2] = Indices[1];
                            NewFace.Indices[1] = Indices[2];

                            // Normals
                            NewFace.Normals[0] = new MVector(Normals[0][0], Normals[0][1], Normals[0][2]);
                            NewFace.Normals[2] = new MVector(Normals[1][0], Normals[1][1], Normals[1][2]);
                            NewFace.Normals[1] = new MVector(Normals[2][0], Normals[2][1], Normals[2][2]);

                            // Colors
                            FaceIterator.getColor(NewFace.Colors[0], 0);
                            FaceIterator.getColor(NewFace.Colors[2], 1);
                            FaceIterator.getColor(NewFace.Colors[1], 2);

                            // Append UV Layers
                            NewFace.UVs[0] = new Tuple<float, float>(UVUs[0], 1 - UVVs[0]);
                            NewFace.UVs[2] = new Tuple<float, float>(UVUs[1], 1 - UVVs[1]);
                            NewFace.UVs[1] = new Tuple<float, float>(UVUs[2], 1 - UVVs[2]);

                            // Set material index
                            if (MeshMaterials.Count > 0)
                                NewFace.MaterialIndex = UsedMaterials[MeshMaterials[0].Name];

                            // Add it
                            NewMesh.Faces.Add(NewFace);
                        }
                        else
                        {
                            // Create new faces
                            FaceVertex NewFace = new FaceVertex(), NewFace2 = new FaceVertex();
                            // Setup
                            NewFace.Indices[0] = Indices[0];
                            NewFace.Indices[2] = Indices[1];
                            NewFace.Indices[1] = Indices[2];
                            NewFace2.Indices[0] = Indices[0];
                            NewFace2.Indices[2] = Indices[2];
                            NewFace2.Indices[1] = Indices[3];

                            // Normals
                            NewFace.Normals[0] = new MVector(Normals[0][0], Normals[0][1], Normals[0][2]);
                            NewFace.Normals[2] = new MVector(Normals[1][0], Normals[1][1], Normals[1][2]);
                            NewFace.Normals[1] = new MVector(Normals[2][0], Normals[2][1], Normals[2][2]);
                            NewFace2.Normals[0] = new MVector(Normals[0][0], Normals[0][1], Normals[0][2]);
                            NewFace2.Normals[2] = new MVector(Normals[2][0], Normals[2][1], Normals[2][2]);
                            NewFace2.Normals[1] = new MVector(Normals[3][0], Normals[3][1], Normals[3][2]);

                            // Colors
                            FaceIterator.getColor(NewFace.Colors[0], 0);
                            FaceIterator.getColor(NewFace.Colors[2], 1);
                            FaceIterator.getColor(NewFace.Colors[1], 2);
                            FaceIterator.getColor(NewFace2.Colors[0], 0);
                            FaceIterator.getColor(NewFace2.Colors[2], 2);
                            FaceIterator.getColor(NewFace2.Colors[1], 3);

                            // Append UV Layers
                            NewFace.UVs[0] = new Tuple<float, float>(UVUs[0], 1 - UVVs[0]);
                            NewFace.UVs[2] = new Tuple<float, float>(UVUs[1], 1 - UVVs[1]);
                            NewFace.UVs[1] = new Tuple<float, float>(UVUs[2], 1 - UVVs[2]);
                            NewFace2.UVs[0] = new Tuple<float, float>(UVUs[0], 1 - UVVs[0]);
                            NewFace2.UVs[2] = new Tuple<float, float>(UVUs[2], 1 - UVVs[2]);
                            NewFace2.UVs[1] = new Tuple<float, float>(UVUs[3], 1 - UVVs[3]);

                            // Set material index
                            if (MeshMaterials.Count > 0)
                            {
                                NewFace.MaterialIndex = UsedMaterials[MeshMaterials[0].Name];
                                NewFace2.MaterialIndex = UsedMaterials[MeshMaterials[0].Name];
                            }

                            // Add it
                            NewMesh.Faces.Add(NewFace);
                            NewMesh.Faces.Add(NewFace2);
                        }
                    }

                    // Add it
                    Result.Meshes.Add(NewMesh);
                }

                // Write
                switch (FileType)
                {
                    case XModelType.Export:
                        Result.WriteExport(FilePath);
                        break;
                    case XModelType.Bin:
                        Result.WriteBin(FilePath);
                        break;
                }
            }

            // Log complete
            MGlobal.displayInfo(string.Format("[CODTools] Exported {0}", System.IO.Path.GetFileName(FilePath)));
        }

        private static MFnSkinCluster FindSkinCluster(ref List<MFnSkinCluster> Clusters, MFnMesh Mesh)
        {
            // Search
            foreach (var Skin in Clusters)
            {
                if (Skin.numOutputConnections == 0)
                    continue;

                for (uint i = 0; i < Skin.numOutputConnections; i++)
                {
                    var GeoPath = new MDagPath();
                    Skin.getPathAtIndex(i, GeoPath);

                    if (GeoPath.equalEqual(Mesh.dagPath))
                        return Skin;
                }
            }

            // None
            return null;
        }

        private static List<Material> GetMaterialsMesh(ref MFnMesh Mesh, ref MDagPath Path)
        {
            // Get materials for this mesh
            var Result = new List<Material>();

            // Fetch data
            var Shaders = new MObjectArray();
            var ShaderIndices = new MIntArray();
            Mesh.getConnectedShaders(Path.instanceNumber, Shaders, ShaderIndices);

            // Iterate and add
            for (int i = 0; i < (int)Shaders.length; i++)
            {
                // Find plug
                var ShaderNode = new MFnDependencyNode(Shaders[i]);
                var ShaderPlug = ShaderNode.findPlug("surfaceShader");
                var MatPlug = new MPlugArray();
                ShaderPlug.connectedTo(MatPlug, true, false);

                if (MatPlug.length > 0)
                    Result.Add(new Material(CleanNodeName(new MFnDependencyNode(MatPlug[0].node).name)));
            }

            return Result;
        }

        private static List<MFnSkinCluster> GetSkinClusters()
        {
            // Gets the scene skin clusters
            var Clusters = new List<MFnSkinCluster>();
            
            //
            // We must steal this because Maya devs are retarded...
            //
            var ObjectType = Type.GetType("Autodesk.Maya.OpenMaya.MFn, openmayacs");
            var EnumType = Enum.Parse(ObjectType.GetNestedType("Type"), "kSkinClusterFilter");
            // 
            // Convert it
            //
            var SearchNode = (MFn.Type)(EnumType);

            var Iter = new MItDependencyNodes(SearchNode);
            for (; !Iter.isDone; Iter.next())
                Clusters.Add(new MFnSkinCluster(Iter.thisNode));

            // Return it
            return Clusters;
        }

        private static void MarkCosmetic(ref List<Bone> Tree, int LevelIndex)
        {
            // Fetch
            var JointsWithParent = Tree.FindAll(x => x.ParentIndex == LevelIndex);

            if (JointsWithParent != null)
            {
                foreach (var Joint in JointsWithParent)
                {
                    // It's a cosmetic
                    Joint.isCosmetic = true;

                    // Mark it's children
                    MarkCosmetic(ref Tree, Tree.IndexOf(Joint));
                }
            }
        }

        private static void SortJoints(ref XModel Model, List<string> ParentStack, string CosmeticRoot = "")
        {
            // Handle no bones
            if (Model.Bones.Count == 0)
                Model.Bones.Add(new Bone("tag_origin"));

            // Prepare to sort, first, assign parent index, if any
            for (int i = 0; i < ParentStack.Count; i++)
            {
                // Assign parent
                Model.Bones[i].ParentIndex = Model.Bones.FindIndex(x => x.TagName == ParentStack[i]);
            }

            // Build root tree
            var RootJointList = Model.Bones.Where(x => x.ParentIndex == -1);
            if (RootJointList.Count() == 0)
            {
                MGlobal.displayError("[CODTools] No root joint was found...");
                return;
            }
            else if (RootJointList.Count() > 1)
            {
                MGlobal.displayError("[CODTools] Multiple root joints not supported in Call of Duty...");
                return;
            }
            var RootJoint = RootJointList.FirstOrDefault();

            // Prepare root tree
            var RootTree = new List<Bone>() { RootJoint };
            BuildRootTree(RootJoint, ref RootTree, ref Model);

            // Mark cosmetic bones using the root
            if (CosmeticRoot != string.Empty)
            {
                // Fetch it if it's in the list
                var CosmeticBone = RootTree.Find(x => x.TagName == CosmeticRoot.Trim());
                if (CosmeticBone != null)
                {
                    // Recursive iterate through children until marked
                    MarkCosmetic(ref RootTree, RootTree.IndexOf(CosmeticBone));
                }
            }

            // Ensure cosmetics are at the end of the tree
            var SortedCosmetic = RootTree.OrderBy(x => x.isCosmetic).ToList();
            var SortedDict = new Dictionary<int, int>();

            // Iterate and resort
            foreach (var Sorted in SortedCosmetic)
            {
                if (Sorted.ParentIndex == -1)
                    continue;

                Sorted.ParentIndex = SortedCosmetic.FindIndex(x => x.TagName == RootTree[Sorted.ParentIndex].TagName);
            }

            // Assign final sorted indicies
            Model.Bones = SortedCosmetic;
        }

        private static void BuildRootTree(Bone Parent, ref List<Bone> RootTree, ref XModel Model)
        {
            // Find bones with this joint as a parent and add
            var ThisBoneIndex = Model.Bones.IndexOf(Parent);

            var JointsWithThis = Model.Bones.Where(x => x.ParentIndex == ThisBoneIndex);
            var ThisBoneNewIndex = RootTree.Count - 1;
            foreach (var Joint in JointsWithThis)
            {
                // Assign
                var Sorted = Joint.ShallowCopy();
                Sorted.ParentIndex = ThisBoneNewIndex;
                RootTree.Add(Sorted);

                // Recursive
                BuildRootTree(Joint, ref RootTree, ref Model);
            }
        }

        internal static MDagPath GetObjectDagPath(MObject Object)
        {
            var SelectionList = new MSelectionList();
            SelectionList.add(new MFnDagNode(Object).fullPathName);

            var Result = new MDagPath();
            SelectionList.getDagPath(0, Result);

            return Result;
        }

        private static string GetParentName(MFnIkJoint Joint)
        {
            // Attempt to result
            var FullPath = Joint.fullPathName;
            var SplitPath = FullPath.Substring(1).Split('|');

            if (SplitPath.Length > 2)
            {
                // We have parents, fetch second to last
                return CleanNodeName(SplitPath[SplitPath.Length - 2]);
            }
            else if (SplitPath.Length == 2)
            {
                // We have parents, ensure this is a joint parent
                var SelectList = new MSelectionList();
                SelectList.add(FullPath.Substring(0, FullPath.IndexOf("|", 1)));

                // Grab it
                var Result = new MDagPath();
                SelectList.getDagPath(0, Result);

                // Check
                if (Result.hasFn(MFn.Type.kJoint))
                    return CleanNodeName(SplitPath[SplitPath.Length - 2]);
            }

            // Root bone
            return string.Empty;
        }

        internal static string CleanNodeName(string Name)
        {
            if (Name.Contains(":"))
                Name = Name.Substring(Name.LastIndexOf(":") + 1);

            return Name;
        }
    }
}
