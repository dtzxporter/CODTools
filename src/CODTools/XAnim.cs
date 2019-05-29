using Autodesk.Maya.OpenMaya;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace CODTools
{
    internal class Notetrack
    {
        public string Name { get; set; }
        public int Frame { get; set; }

        public Notetrack(string Name, int Frame)
        {
            // Set it
            this.Name = Name;
            this.Frame = Frame;
        }
    }

    internal class PartFrame
    {
        public MVector Offset { get; set; }
        public MMatrix RotationMatrix { get; set; }

        public PartFrame()
        {
            // Defaults
            this.Offset = new MVector(MVector.zero);
            this.RotationMatrix = new MMatrix(MMatrix.identity);
        }
    }

    internal class Part
    {
        public string TagName { get; set; }
        public List<PartFrame> Frames { get; set; }

        public Part(string Name)
        {
            // Set it
            this.TagName = Name;

            // Initialize
            this.Frames = new List<PartFrame>();
        }
    }

    internal class XAnim
    {
        public string Name { get; set; }
        public List<string> Comments { get; set; }

        public List<Part> Parts { get; set; }
        public List<Notetrack> Notetracks { get; set; }

        public void ValidateXAnim()
        {
            // Perform basic validation
            foreach (var Note in Notetracks)
                Note.Name = Note.Name.Trim();

            // No bad notetracks "end"
            this.Notetracks = this.Notetracks.Where(x => x.Name != "end").ToList<Notetrack>();
        }

        public int FrameCount()
        {
            // Return frames
            if (this.Parts.Count > 0)
                return this.Parts[0].Frames.Count;

            // None
            return 0;
        }

        public void WriteSiegeSource(string FilePath)
        {
            // Validate anim
            this.ValidateXAnim();

            // Prepare to write the source file
            var Result = new SiegeAnim();

            // Set basic flags
            var FrameCount = this.FrameCount();

            if (FrameCount <= 0)
                return;

            // Set it
            Result.animation.frames = FrameCount;
            Result.animation.loop = 1;
            Result.animation.nodes = this.Parts.Count;
            Result.animation.playbackSpeed = 1;
            Result.animation.speed = 0;

            // Default shot
            Result.shots.Add(new Shot() { start = 0, end = FrameCount, name = "default" });

            // Default info
            Result.info.argJson = "{}";
            Result.info.computer = "D3V-137";
            Result.info.domain = "ATVI";
            Result.info.ta_game_path = "c:\\";
            Result.info.time = "";
            Result.info.user = "d3v";

            // Prepare to generate siege data
            foreach (var Part in this.Parts)
                Result.nodes.Add(new Node() { name = Part.TagName });

            using (var Zip = ZipStorer.Create(FilePath, string.Empty))
            {
                using (var WritePosition = new BinaryWriter(new MemoryStream()))
                using (var WriteRotation = new BinaryWriter(new MemoryStream()))
                {
                    // Loop over frames, then parts, and write data
                    for (int i = 0; i < FrameCount; i++)
                    {
                        foreach (var Part in this.Parts)
                        {
                            // Get rotation as quaternion
                            var RotationQuat = new MTransformationMatrix(Part.Frames[i].RotationMatrix).rotation;

                            // Write rotation and position raw
                            WritePosition.Write((float)Part.Frames[i].Offset.x);
                            WritePosition.Write((float)Part.Frames[i].Offset.y);
                            WritePosition.Write((float)Part.Frames[i].Offset.z);

                            WriteRotation.Write((float)RotationQuat.x);
                            WriteRotation.Write((float)RotationQuat.y);
                            WriteRotation.Write((float)RotationQuat.z);
                            WriteRotation.Write((float)RotationQuat.w);
                        }
                    }

                    // Flush
                    WritePosition.Flush();
                    WriteRotation.Flush();

                    // Reset
                    WritePosition.BaseStream.Position = 0;
                    WriteRotation.BaseStream.Position = 0;

                    // Add to archive
                    Zip.AddStream(ZipStorer.Compression.Deflate, "data/positions", WritePosition.BaseStream, DateTime.Now, string.Empty);
                    Zip.AddStream(ZipStorer.Compression.Deflate, "data/quaternions", WriteRotation.BaseStream, DateTime.Now, string.Empty);

                    // Set strides
                    Result.data.dataPositionsBase.byteStride = 12 * this.Parts.Count;
                    Result.data.dataPositionsBase.byteSize = 12 * this.Parts.Count * FrameCount;
                    Result.data.dataRotationsBase.byteStride = 16 * this.Parts.Count;
                    Result.data.dataRotationsBase.byteSize = 16 * this.Parts.Count * FrameCount;

                    // Serialize to file
                    var SourceMod = new JavaScriptSerializer().Serialize(Result);
                    // Remove the broken names
                    SourceMod = SourceMod.Replace("dataRotationsBase", "data/quaternions").Replace("dataPositionsBase", "data/positions");
                    
                    // Add to archive
                    using (var Index = new MemoryStream(Encoding.UTF8.GetBytes(SourceMod)))
                        Zip.AddStream(ZipStorer.Compression.Deflate, "index.json", Index, DateTime.Now, string.Empty);
                }
            }
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
            // Validate anim
            this.ValidateXAnim();

            // Begin writer
            var Writer = new StreamWriter(Stream);

            // Metadata
            foreach (var Comment in this.Comments)
                Writer.WriteLine("// {0}", Comment);
            if (this.Comments.Count > 0)
                Writer.WriteLine();

            // Required format version
            Writer.WriteLine("ANIMATION\nVERSION {0}\n", 3);

            Writer.WriteLine("NUMPARTS {0}", this.Parts.Count);

            // Part list
            for (int i = 0; i < this.Parts.Count; i++)
            {
                Writer.WriteLine("PART {0} \"{1}\"", i, this.Parts[i].TagName);
            }
            Writer.WriteLine();

            // Framerate and count
            Writer.WriteLine("FRAMERATE 30\nNUMFRAMES {0}\n", this.FrameCount());

            if (this.Parts.Count > 0)
            {
                // Iterate over frames
                for (int i = 0; i < this.Parts[0].Frames.Count; i++)
                {
                    Writer.WriteLine("FRAME {0}\n", i);
                    // Iterate over parts
                    for (int p = 0; p < this.Parts.Count; p++)
                    {
                        Writer.WriteLine("PART {0}", p);
                        Writer.WriteLine("OFFSET {0}, {1}, {2}", this.Parts[p].Frames[i].Offset.x.ToRoundedFloat(), this.Parts[p].Frames[i].Offset.y.ToRoundedFloat(), this.Parts[p].Frames[i].Offset.z.ToRoundedFloat());
                        Writer.WriteLine("X {0}, {1}, {2}", this.Parts[p].Frames[i].RotationMatrix[0, 0].ToRoundedFloat(), this.Parts[p].Frames[i].RotationMatrix[0, 1].ToRoundedFloat(), this.Parts[p].Frames[i].RotationMatrix[0, 2].ToRoundedFloat());
                        Writer.WriteLine("Y {0}, {1}, {2}", this.Parts[p].Frames[i].RotationMatrix[1, 0].ToRoundedFloat(), this.Parts[p].Frames[i].RotationMatrix[1, 1].ToRoundedFloat(), this.Parts[p].Frames[i].RotationMatrix[1, 2].ToRoundedFloat());
                        Writer.WriteLine("Z {0}, {1}, {2}", this.Parts[p].Frames[i].RotationMatrix[2, 0].ToRoundedFloat(), this.Parts[p].Frames[i].RotationMatrix[2, 1].ToRoundedFloat(), this.Parts[p].Frames[i].RotationMatrix[2, 2].ToRoundedFloat());
                        Writer.WriteLine();
                    }
                }
            }

            // Output count
            Writer.WriteLine("NUMKEYS {0}", this.Notetracks.Count);
            // Iterate and output
            foreach (var Key in Notetracks)
            {
                // Make sure we escape quotes here so that every compiler can read them!
                Writer.WriteLine("FRAME {0} \"{1}\"", Key.Frame, Key.Name.Replace("\"", "\\\""));
            }

            // Flush writer
            Writer.Flush();
        }

        public XAnim(string Name)
        {
            // Set it
            this.Name = Name;

            // Initialize
            this.Comments = new List<string>();
            this.Parts = new List<Part>();
            this.Notetracks = new List<Notetrack>();
        }
    }
}
