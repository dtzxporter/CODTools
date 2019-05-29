using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Maya.OpenMaya;
using Autodesk.Maya.OpenMayaAnim;
using System.IO;
using System.Threading;
using System.Globalization;
using System.Threading.Tasks;

namespace CODTools
{
    internal class Bone
    {
        public string TagName { get; set; }
        public bool isCosmetic { get; set; }
        public int ParentIndex { get; set; }

        public MVector Translation { get; set; }
        public MVector Scale { get; set; }
        public MMatrix RotationMatrix { get; set; }

        public Bone(string Name)
        {
            // Set it
            this.TagName = Name;

            // Default transform
            this.Translation = new MVector(MVector.zero);
            this.Scale = new MVector(MVector.one);
            this.RotationMatrix = new MMatrix(MMatrix.identity);
            this.ParentIndex = -1;
            this.isCosmetic = false;
        }

        public Bone ShallowCopy()
        {
            return (Bone)this.MemberwiseClone();
        }
    }

    internal class Vertex
    {
        public MPoint Position { get; set; }
        public List<Tuple<int, float>> Weights { get; set; }

        public Vertex()
        {
            this.Position = MPoint.origin;
            this.Weights = new List<Tuple<int, float>>();
        }
    }

    internal class FaceVertex
    {
        public int[] Indices { get; set; }
        public MVector[] Normals { get; set; }
        public MColor[] Colors { get; set; }
        public Tuple<float, float>[] UVs { get; set; }
        public int MaterialIndex { get; set; }

        public FaceVertex()
        {
            this.Indices = new int[3] { 0, 0, 0 };
            this.Normals = new MVector[3] { new MVector(MVector.zero), new MVector(MVector.zero), new MVector(MVector.zero) };
            this.Colors = new MColor[3] { new MColor(255f, 255f, 255f, 255f), new MColor(255f, 255f, 255f, 255f), new MColor(255f, 255f, 255f, 255f) };
            this.UVs = new Tuple<float, float>[3];
            this.MaterialIndex = 0;
        }
    }

    internal class Mesh
    {
        public List<Vertex> Vertices { get; set; }
        public List<FaceVertex> Faces { get; set; }

        public Mesh()
        {
            this.Vertices = new List<Vertex>();
            this.Faces = new List<FaceVertex>();
        }
    }

    internal class Material
    {
        public string Name { get; set; }
        public string Diffuse { get; set; }

        public Material(string Name)
        {
            this.Name = Name;
            this.Diffuse = "default.dds";
        }
    }

    internal class XModel
    {
        public string Name { get; set; }
        public List<string> Comments { get; set; }

        public bool SiegeModel { get; set; }

        public List<Bone> Bones { get; set; }
        public List<Mesh> Meshes { get; set; }
        public List<Material> Materials { get; set; }

        public XModel(string Name)
        {
            // Set it
            this.Name = Name;

            // Initialize
            this.Comments = new List<string>();
            this.Bones = new List<Bone>();
            this.Meshes = new List<Mesh>();
            this.Materials = new List<Material>();

            // Not a siege model
            this.SiegeModel = false;
        }

        public int VertexCount()
        {
            int Result = 0;

            foreach (var Mesh in Meshes)
                Result += Mesh.Vertices.Count;

            return Result;
        }

        public int FaceCount()
        {
            int Result = 0;

            foreach (var Mesh in Meshes)
                Result += Mesh.Faces.Count;

            return Result;
        }

        public Dictionary<string, int> GetBoneMapping()
        {
            var Result = new Dictionary<string, int>();

            for (int i = 0; i < this.Bones.Count; i++)
                Result.Add(this.Bones[i].TagName, i);

            return Result;
        }

        public void ValidateXModel()
        {
            // Perform basic validation, adding root bone, having a material
            if (this.Bones.Count == 0)
                this.Bones.Add(new Bone("tag_origin"));
            if (this.Materials.Count == 0)
                this.Materials.Add(new Material("default"));
        }

        public void WriteBin(string FilePath)
        {
            // Write to a memory file, then ask the XAssetFile to convert it
            using (var Memory = new MemoryStream())
            {
                // Create
                this.WriteExport(Memory);

                // Reset
                Memory.Position = 0;

                // Write it
                using (var XAsset = new XAssetFile(Memory, InFormat.Export))
                    XAsset.WriteBin(FilePath);
            }
        }

        public void WriteExport(string FilePath)
        {
            using (var Fs = File.Create(FilePath))
                this.WriteExport(Fs);
        }

        public void WriteExport(Stream Stream)
        {
            // Validate model
            this.ValidateXModel();

            // Begin writer
            var Writer = new StreamWriter(Stream);

            // Metadata
            foreach (var Comment in this.Comments)
                Writer.WriteLine("// {0}", Comment);
            if (this.Comments.Count > 0)
                Writer.WriteLine();

            // Required format version
            Writer.WriteLine("MODEL\nVERSION {0}\n", 6);

            // If we are a siege model, skip default bones
            if (this.SiegeModel)
            {
                // Skip default, inject a dummy bone
                Writer.WriteLine("NUMBONES 1\nBONE 0 -1 \"{0}\"\n\nBONE 0\nOFFSET 0.000000, 0.000000, 0.000000\nX 1.000000, 0.000000, 0.000000\nY 0.000000, 1.000000, 0.000000\nZ 0.000000, 0.000000, 1.000000\n", this.Name);
            }
            else
            {
                // Bones
                var CosmeticCount = this.Bones.Where(x => x.isCosmetic).Count();
                if (CosmeticCount > 0)
                    Writer.WriteLine("NUMCOSMETICS {0}", CosmeticCount);
                Writer.WriteLine("NUMBONES {0}", this.Bones.Count);

                // Parent list
                for (int i = 0; i < this.Bones.Count; i++)
                {
                    Writer.WriteLine("BONE {0} {1} \"{2}\"", i, this.Bones[i].ParentIndex, this.Bones[i].TagName);
                }
                Writer.WriteLine();

                // Bone information
                for (int i = 0; i < this.Bones.Count; i++)
                {
                    Writer.WriteLine("BONE {0}", i);
                    Writer.WriteLine("OFFSET {0}, {1}, {2}", this.Bones[i].Translation.x.ToRoundedFloat(), this.Bones[i].Translation.y.ToRoundedFloat(), this.Bones[i].Translation.z.ToRoundedFloat());
                    Writer.WriteLine("SCALE {0}, {1}, {2}", this.Bones[i].Scale.x.ToRoundedFloat(), this.Bones[i].Scale.y.ToRoundedFloat(), this.Bones[i].Scale.z.ToRoundedFloat());
                    Writer.WriteLine("X {0}, {1}, {2}", this.Bones[i].RotationMatrix[0, 0].ToRoundedFloat(), this.Bones[i].RotationMatrix[0, 1].ToRoundedFloat(), this.Bones[i].RotationMatrix[0, 2].ToRoundedFloat());
                    Writer.WriteLine("Y {0}, {1}, {2}", this.Bones[i].RotationMatrix[1, 0].ToRoundedFloat(), this.Bones[i].RotationMatrix[1, 1].ToRoundedFloat(), this.Bones[i].RotationMatrix[1, 2].ToRoundedFloat());
                    Writer.WriteLine("Z {0}, {1}, {2}", this.Bones[i].RotationMatrix[2, 0].ToRoundedFloat(), this.Bones[i].RotationMatrix[2, 1].ToRoundedFloat(), this.Bones[i].RotationMatrix[2, 2].ToRoundedFloat());
                    Writer.WriteLine();
                }
            }

            // Mesh vertex buffer
            var VertexBufferSize = this.VertexCount();
            var FaceBufferSize = this.FaceCount();

            // Global offsets
            int VertexIndex = 0, FaceIndex = 0, MeshIndex = 0;

            // Write count
            Writer.WriteLine("{0} {1}", (VertexBufferSize > ushort.MaxValue) ? "NUMVERTS32" : "NUMVERTS", VertexBufferSize);
            // Iterate and output
            foreach (var Mesh in this.Meshes)
            {
                foreach (var Vertex in Mesh.Vertices)
                {
                    // Output position, weight data
                    Writer.WriteLine("{0} {1}", (VertexIndex > ushort.MaxValue) ? "VERT32" : "VERT", VertexIndex++);
                    Writer.WriteLine("OFFSET {0}, {1}, {2}", Vertex.Position.x.ToRoundedFloat(), Vertex.Position.y.ToRoundedFloat(), Vertex.Position.z.ToRoundedFloat());

                    // Weights, default weight when siege model is active
                    if (!this.SiegeModel)
                    {
                        Writer.WriteLine("BONES {0}", Vertex.Weights.Count);
                        foreach (var Weight in Vertex.Weights)
                            Writer.WriteLine("BONE {0} {1}", Weight.Item1, Weight.Item2.ToRoundedFloat());
                    }
                    else
                    {
                        Writer.WriteLine("BONES 1\nBONE 0 1.000000");
                    }
                }
            }
            if (this.Meshes.Count > 0)
                Writer.WriteLine();

            // Write count
            Writer.WriteLine("NUMFACES {0}", FaceBufferSize);
            // Iterate and output
            foreach (var Mesh in this.Meshes)
            {
                foreach (var Face in Mesh.Faces)
                {
                    // Triangle data
                    Writer.WriteLine("{0} {1} {2} 0 0", (MeshIndex > byte.MaxValue || Face.MaterialIndex > byte.MaxValue) ? "TRI16" : "TRI", MeshIndex, Face.MaterialIndex);

                    // Output triangle vert data
                    for (int i = 0; i < 3; i++)
                    {
                        Writer.WriteLine("{0} {1}", ((Face.Indices[i] + FaceIndex) > ushort.MaxValue) ? "VERT32" : "VERT", Face.Indices[i] + FaceIndex);
                        Writer.WriteLine("NORMAL {0} {1} {2}", Face.Normals[i].x.ToRoundedFloat(), Face.Normals[i].y.ToRoundedFloat(), Face.Normals[i].z.ToRoundedFloat());
                        Writer.WriteLine("COLOR {0} {1} {2} {3}", Face.Colors[i].r.ToRoundedFloat(), Face.Colors[i].g.ToRoundedFloat(), Face.Colors[i].b.ToRoundedFloat(), Face.Colors[i].a.ToRoundedFloat());
                        Writer.WriteLine("UV 1 {0} {1}", Face.UVs[i].Item1.ToRoundedFloat(), Face.UVs[i].Item2.ToRoundedFloat());
                    }
                }

                // Advance
                FaceIndex += Mesh.Vertices.Count;
                MeshIndex++;
            }
            if (this.Meshes.Count > 0)
                Writer.WriteLine();

            MeshIndex = 0;

            // Write count
            Writer.WriteLine("NUMOBJECTS {0}", this.Meshes.Count);
            // Iterate and output
            foreach (var Mesh in this.Meshes)
            {
                Writer.WriteLine("OBJECT {0} \"CODToolsMesh_{1}\"", MeshIndex, MeshIndex++);
            }
            if (this.Meshes.Count > 0)
                Writer.WriteLine();

            int MatIndex = 0;

            // Write count
            Writer.WriteLine("NUMMATERIALS {0}", this.Materials.Count);
            // Iterate and output
            foreach (var Mat in this.Materials)
            {
                Writer.WriteLine("MATERIAL {0} \"{1}\" \"Phong\" \"color:{2}\"", MatIndex++, Mat.Name, Mat.Diffuse);
                Writer.WriteLine("COLOR 0.000000 0.000000 0.000000 1.000000\nTRANSPARENCY 0.000000 0.000000 0.000000 1.000000\nAMBIENTCOLOR 0.000000 0.000000 0.000000 1.000000\nINCANDESCENCE 0.000000 0.000000 0.000000 1.000000\nCOEFFS 0.800000 0.000000\nGLOW 0.000000 0\nREFRACTIVE 6 1.000000\nSPECULARCOLOR -1.000000 -1.000000 -1.000000 1.000000\nREFLECTIVECOLOR -1.000000 -1.000000 -1.000000 1.000000\nREFLECTIVE -1 -1.000000\nBLINN -1.000000 -1.000000\nPHONG -1.000000");
            }

            // If we're a siege model
            if (this.SiegeModel)
            {
                Writer.WriteLine();

                // Write actual vertex weights here
                VertexIndex = 0;
                // Write count
                Writer.WriteLine("NUMSBONES {0}", this.Bones.Count);
                // Iterate
                for (int i = 0; i < this.Bones.Count; i++)
                {
                    // Get rotation as quaternion
                    var RotationQuat = new MTransformationMatrix(this.Bones[i].RotationMatrix).rotation;

                    Writer.WriteLine("BONE {0} -1 \"{1}\"", i, this.Bones[i].TagName);
                    Writer.WriteLine("OFFSET {0}, {1}, {2}", this.Bones[i].Translation.x.ToRoundedFloat(), this.Bones[i].Translation.y.ToRoundedFloat(), this.Bones[i].Translation.z.ToRoundedFloat());
                    Writer.WriteLine("QUATERNION {0}, {1}, {2}, {3}", RotationQuat.x.ToRoundedFloat(), RotationQuat.y.ToRoundedFloat(), RotationQuat.z.ToRoundedFloat(), RotationQuat.w.ToRoundedFloat());
                    Writer.WriteLine();
                }
                Writer.WriteLine();

                // Prepare to write the weights
                Writer.WriteLine("NUMSWEIGHTS {0}", VertexBufferSize);
                // Iterate and output
                foreach (var Mesh in this.Meshes)
                {
                    foreach (var Vertex in Mesh.Vertices)
                    {
                        // Output position, weight data
                        Writer.WriteLine("{0} {1}", (VertexIndex > ushort.MaxValue) ? "VERT32" : "VERT", VertexIndex++);

                        // Weights
                        Writer.WriteLine("BONES {0}", Vertex.Weights.Count);
                        foreach (var Weight in Vertex.Weights)
                            Writer.WriteLine("BONE {0} {1}", Weight.Item1, Weight.Item2.ToRoundedFloat());
                    }
                }
            }

            // Flush writer
            Writer.Flush();
        }
    }
}
