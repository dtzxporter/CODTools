using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CODTools
{
    // Reader and writer delegates
    internal delegate void XBinBlockReader(XBlock Block, BinaryReader Reader);
    internal delegate void XBinBlockWriter(XBlock Block, BinaryWriter Writer);
    internal delegate void XExportBlockReader(XBlock Block, string CurrentLine, string[] SplitLine);
    internal delegate void XExportBlockWriter(XBlock Block, StreamWriter Writer);

    internal class XBlock : ICloneable
    {
        // Block info
        public ushort BlockID { get; set; }
        public string DisplayName { get; set; }
        public string TextID { get; set; }
        // Read and write handlers
        public XBinBlockReader BinReader { get; set; }
        public XExportBlockReader ExportReader { get; set; }
        public XBinBlockWriter BinWriter { get; set; }
        public XExportBlockWriter ExportWriter { get; set; }
        // Block data
        public object BlockData { get; set; }

        // Clone a XBlock object
        public object Clone()
        {
            // Clone us
            return this.MemberwiseClone();
        }
    }

    internal enum InFormat
    {
        Export,
        Bin
    }

    /// <summary>
    /// A structure containing an xasset (Model or anim) from COD
    /// </summary>
    internal class XAssetFile : IDisposable
    {
        /// <summary>
        /// A list of file blocks that make up the asset
        /// </summary>
        public List<XBlock> FileBlocks { get; set; }
        /// <summary>
        /// The last error line (or offset) where an issue occured
        /// </summary>
        public long ErrorLine { get; set; }
        /// <summary>
        /// The format that we imported from
        /// </summary>
        public InFormat Format { get; set; }

        // Pad a length to boundary
        private static long Padded(long Size) { return (Size + 0x3) & 0xFFFFFFFFFFFFFC; }

        // Pad a binarywriter with bytes
        private static void PadStream(BinaryWriter Writer, int PadSize) { Writer.Write(new byte[PadSize]); }

        // Clamp a number to bounds
        private static double Clamp(double Num, double Lower, double Upper) { return Math.Max(Lower, Math.Min(Num, Upper)); }

        #region Utilities

        private static string XBin_ReadAlignedString(BinaryReader Reader)
        {
            // Get start
            long StartingPosition = Reader.BaseStream.Position;
            // Read it
            string Result = Reader.ReadNullTerminatedString();
            // Skip over padding
            Reader.BaseStream.Position = (StartingPosition + Padded(Reader.BaseStream.Position - StartingPosition));
            // Return it
            return Result;
        }

        private static void XBin_WriteAlignedString(BinaryWriter Writer, string ToWrite)
        {
            // Get start
            long StartingPosition = Writer.BaseStream.Position;
            // Get the value
            byte[] Value = Encoding.ASCII.GetBytes(ToWrite);
            // Write it
            Writer.Write(Value);
            // Null-terminate it
            PadStream(Writer, 1);
            // Calculate padding size
            var PaddingSize = Convert.ToInt32((StartingPosition + Padded(Writer.BaseStream.Position - StartingPosition)) - Writer.BaseStream.Position);
            // Pad the stream
            PadStream(Writer, PaddingSize);
        }

        #endregion

        #region Bin Read Handlers

        private static void XBinBlock_Comment_Read(XBlock Block, BinaryReader Reader)
        {
            // Read a comment block
            long StartingPosition = Reader.BaseStream.Position - 2;
            // Read the string (Skip 2)
            Reader.BaseStream.Seek(2, SeekOrigin.Current);
            // Read string
            string Comment = Reader.ReadNullTerminatedString();
            // Skip over comment padding
            Reader.BaseStream.Position = (StartingPosition + Padded(Reader.BaseStream.Position - StartingPosition));
            // Set data
            Block.BlockData = Comment;
        }

        private static void XBinBlock_PartInfo_Read(XBlock Block, BinaryReader Reader)
        {
            // Read a part block
            long StartingPosition = Reader.BaseStream.Position - 2;
            // Read index
            var BoneIndex = Reader.ReadUInt16();
            // Read bone name
            var BoneName = Reader.ReadNullTerminatedString();
            // Skip over comment padding
            Reader.BaseStream.Position = (StartingPosition + Padded(Reader.BaseStream.Position - StartingPosition));
            // Set data
            Block.BlockData = new Tuple<ushort, string>(BoneIndex, BoneName);
        }

        private static void XBinBlock_FrameInfo_Read(XBlock Block, BinaryReader Reader)
        {
            // Read a part block
            long StartingPosition = Reader.BaseStream.Position - 2;
            // Read the frame index (skip 2)
            Reader.BaseStream.Seek(2, SeekOrigin.Current);
            // Read index
            var FrameIndex = Reader.ReadInt32();
            // Read name
            var FrameName = Reader.ReadNullTerminatedString();
            // Skip over comment padding
            Reader.BaseStream.Position = (StartingPosition + Padded(Reader.BaseStream.Position - StartingPosition));
            // Set data
            Block.BlockData = new Tuple<int, string>(FrameIndex, FrameName);
        }

        private static void XBinBlock_Identifier_Read(XBlock Block, BinaryReader Reader)
        {
            // Read a ID block
            Reader.BaseStream.Seek(2, SeekOrigin.Current);
        }

        private static void XBinBlock_UInt16_Read(XBlock Block, BinaryReader Reader)
        {
            // Read a ushort value block
            Block.BlockData = Reader.ReadUInt16();
        }

        private static void XBinBlock_Int32_Read(XBlock Block, BinaryReader Reader)
        {
            // Read a int value block (Skip 2)
            Reader.BaseStream.Seek(2, SeekOrigin.Current);
            // Read value
            Block.BlockData = Reader.ReadInt32();
        }

        private static void XBinBlock_Vec3_Read(XBlock Block, BinaryReader Reader)
        {
            // Read a Vec3 block
            long StartingPosition = Reader.BaseStream.Position - 2;
            // Read the coords (Skip 2)
            Reader.BaseStream.Seek(2, SeekOrigin.Current);
            // Read them
            var CoordX = Reader.ReadSingle();
            var CoordY = Reader.ReadSingle();
            var CoordZ = Reader.ReadSingle();
            // Skip over block padding
            Reader.BaseStream.Position = (StartingPosition + Padded(Reader.BaseStream.Position - StartingPosition));
            // Set data
            Block.BlockData = new Tuple<float, float, float>(CoordX, CoordY, CoordZ);
        }

        private static void XBinBlock_Quat_Read(XBlock Block, BinaryReader Reader)
        {
            // Read a Quat block (Skip 2)
            Reader.BaseStream.Seek(2, SeekOrigin.Current);
            // Read coords
            var CoordX = Reader.ReadSingle();
            var CoordY = Reader.ReadSingle();
            var CoordZ = Reader.ReadSingle();
            var CoordW = Reader.ReadSingle();
            // Set data
            Block.BlockData = new Tuple<float, float, float, float>(CoordX, CoordY, CoordZ, CoordW);
        }

        private static void XBinBlock_ShortVec3_Read(XBlock Block, BinaryReader Reader)
        {
            // Read a Short Vec3 block
            long StartingPosition = Reader.BaseStream.Position - 2;
            // Read them
            var CoordX = (float)Reader.ReadInt16();
            var CoordY = (float)Reader.ReadInt16();
            var CoordZ = (float)Reader.ReadInt16();
            // Skip over block padding
            Reader.BaseStream.Position = (StartingPosition + Padded(Reader.BaseStream.Position - StartingPosition));
            // Set data
            Block.BlockData = new Tuple<float, float, float>(CoordX / 32767.0f, CoordY / 32767.0f, CoordZ / 32767.0f);
        }

        private static void XBinBlock_Weight_Read(XBlock Block, BinaryReader Reader)
        {
            // Read a bone vert weight
            var BoneID = Reader.ReadUInt16();
            // Read value
            var WeightValue = Reader.ReadSingle();
            // Set data
            Block.BlockData = new Tuple<ushort, float>(BoneID, WeightValue);
        }

        private static void XBinBlock_FaceInfo_Read(XBlock Block, BinaryReader Reader)
        {
            // Read a face info
            var SubmeshIndex = Reader.ReadByte();
            // Read material index
            var MaterialIndex = Reader.ReadByte();
            // Set data
            Block.BlockData = new Tuple<byte, byte>(SubmeshIndex, MaterialIndex);
        }

        private static void XBinBlock_FaceInfo16_Read(XBlock Block, BinaryReader Reader)
        {
            // Skip 2 bytes padding
            Reader.BaseStream.Seek(2, SeekOrigin.Current);
            // Read a face info
            var SubmeshIndex = Reader.ReadUInt16();
            // Read material index
            var MaterialIndex = Reader.ReadUInt16();
            // Set data
            Block.BlockData = new Tuple<ushort, ushort>(SubmeshIndex, MaterialIndex);
        }

        private static void XBinBlock_Vec4_Read(XBlock Block, BinaryReader Reader)
        {
            // Read a Vec4 block
            long StartingPosition = Reader.BaseStream.Position - 2;
            // Read the XYZW values (Skip 2)
            Reader.BaseStream.Seek(2, SeekOrigin.Current);
            // Read them
            var CoordX = Reader.ReadSingle();
            var CoordY = Reader.ReadSingle();
            var CoordZ = Reader.ReadSingle();
            var CoordW = Reader.ReadSingle();
            // Skip over block padding
            Reader.BaseStream.Position = (StartingPosition + Padded(Reader.BaseStream.Position - StartingPosition));
            // Set data
            Block.BlockData = new Tuple<float, float, float, float>(CoordX, CoordY, CoordZ, CoordW);
        }

        private static void XBinBlock_Vec2_Read(XBlock Block, BinaryReader Reader)
        {
            // Read a Vec2 block
            long StartingPosition = Reader.BaseStream.Position - 2;
            // Read the XY values (Skip 2)
            Reader.BaseStream.Seek(2, SeekOrigin.Current);
            // Read them
            var CoordX = Reader.ReadSingle();
            var CoordY = Reader.ReadSingle();
            // Skip over block padding
            Reader.BaseStream.Position = (StartingPosition + Padded(Reader.BaseStream.Position - StartingPosition));
            // Set data
            Block.BlockData = new Tuple<float, float>(CoordX, CoordY);
        }

        private static void XBinBlock_Float_Read(XBlock Block, BinaryReader Reader)
        {
            // Read a Float block
            long StartingPosition = Reader.BaseStream.Position - 2;
            // Read the X values (Skip 2)
            Reader.BaseStream.Seek(2, SeekOrigin.Current);
            // Read them
            var CoordX = Reader.ReadSingle();
            // Skip over block padding
            Reader.BaseStream.Position = (StartingPosition + Padded(Reader.BaseStream.Position - StartingPosition));
            // Set data
            Block.BlockData = CoordX;
        }

        private static void XBinBlock_BoneInfo_Read(XBlock Block, BinaryReader Reader)
        {
            // Read a bone info block
            long StartingPosition = Reader.BaseStream.Position - 2;
            // Read the index and parent (Skip 2)
            Reader.BaseStream.Seek(2, SeekOrigin.Current);
            // Read them
            var BoneIndex = Reader.ReadInt32();
            var BoneParent = Reader.ReadInt32();
            // Read name
            string BoneName = Reader.ReadNullTerminatedString();
            // Skip over block padding
            Reader.BaseStream.Position = (StartingPosition + Padded(Reader.BaseStream.Position - StartingPosition));
            // Set data
            Block.BlockData = new Tuple<int, int, string>(BoneIndex, BoneParent, BoneName);
        }

        private static void XBinBlock_SubmeshInfo_Read(XBlock Block, BinaryReader Reader)
        {
            // Read a submesh info
            long StartingPosition = Reader.BaseStream.Position - 2;
            // Read the index
            var SubmeshIndex = Reader.ReadUInt16();
            // Read name
            string SubmeshName = Reader.ReadNullTerminatedString();
            // Skip over block padding
            Reader.BaseStream.Position = (StartingPosition + Padded(Reader.BaseStream.Position - StartingPosition));
            // Set data
            Block.BlockData = new Tuple<ushort, string>(SubmeshIndex, SubmeshName);
        }

        private static void XBinBlock_MaterialInfo_Read(XBlock Block, BinaryReader Reader)
        {
            // Read a submesh info
            long StartingPosition = Reader.BaseStream.Position - 2;
            // Read the index
            var MaterialIndex = Reader.ReadUInt16();
            // Read name
            string MaterialName = XBin_ReadAlignedString(Reader);
            // Read type
            string MaterialType = XBin_ReadAlignedString(Reader);
            // Read images
            string MaterialImages = XBin_ReadAlignedString(Reader);
            // Skip over block padding
            Reader.BaseStream.Position = (StartingPosition + Padded(Reader.BaseStream.Position - StartingPosition));
            // Set data
            Block.BlockData = new Tuple<ushort, string, string, string>(MaterialIndex, MaterialName, MaterialType, MaterialImages);
        }

        private static void XBinBlock_VertColor_Read(XBlock Block, BinaryReader Reader)
        {
            // Read a vert color block (Skip 2)
            Reader.BaseStream.Seek(2, SeekOrigin.Current);
            // Read the values
            var ColorR = (float)Reader.ReadByte();
            var ColorG = (float)Reader.ReadByte();
            var ColorB = (float)Reader.ReadByte();
            var ColorA = (float)Reader.ReadByte();
            // Set data
            Block.BlockData = new Tuple<float, float, float, float>(ColorR / 255.0f, ColorG / 255.0f, ColorB / 255.0f, ColorA / 255.0f);
        }

        private static void XBinBlock_VertUV_Read(XBlock Block, BinaryReader Reader)
        {
            // Read a vert UV block
            var UVLayerCount = Reader.ReadUInt16();
            // A list of uvs
            var UVLayers = new List<Tuple<ushort, float, float>>();

            // Loop and read
            for (ushort i = 0; i < UVLayerCount; i++)
            {
                // Values
                var UVU = Reader.ReadSingle();
                var UVV = Reader.ReadSingle();
                // Add
                UVLayers.Add(new Tuple<ushort, float, float>(i, UVU, UVV));
            }

            // Set data
            Block.BlockData = UVLayers;
        }

        #endregion

        #region Bin Write Handlers

        private static void XBinBlock_Comment_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a comment block
            long StartingPosition = Writer.BaseStream.Position;
            // Write ID
            Writer.Write((ushort)Block.BlockID);
            // Pad two bytes
            PadStream(Writer, 2);
            // Build the comment string
            byte[] Comment = Encoding.ASCII.GetBytes((string)Block.BlockData);
            // Write it
            Writer.Write(Comment);
            // Null-term it
            PadStream(Writer, 1);
            // Calculate padding to skip
            int PaddingSize = Convert.ToInt32((StartingPosition + Padded(Writer.BaseStream.Position - StartingPosition)) - Writer.BaseStream.Position);
            // Pad
            PadStream(Writer, PaddingSize);
        }

        private static void XBinBlock_MaterialInfo_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a comment block
            long StartingPosition = Writer.BaseStream.Position;
            // Write ID
            Writer.Write((ushort)Block.BlockID);
            // Write material index
            Writer.Write((ushort)((Tuple<ushort, string, string, string>)Block.BlockData).Item1);
            // Write material name (padded)
            XBin_WriteAlignedString(Writer, ((Tuple<ushort, string, string, string>)Block.BlockData).Item2);
            // Write material type (padded)
            XBin_WriteAlignedString(Writer, ((Tuple<ushort, string, string, string>)Block.BlockData).Item3);
            // Write material images (padded)
            XBin_WriteAlignedString(Writer, ((Tuple<ushort, string, string, string>)Block.BlockData).Item4);
            // Calculate padding to skip
            int PaddingSize = Convert.ToInt32((StartingPosition + Padded(Writer.BaseStream.Position - StartingPosition)) - Writer.BaseStream.Position);
            // Pad
            PadStream(Writer, PaddingSize);
        }

        private static void XBinBlock_SubmeshInfo_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a submesh block
            long StartingPosition = Writer.BaseStream.Position;
            // Write ID
            Writer.Write((ushort)Block.BlockID);
            // Write the submesh index
            Writer.Write((ushort)((Tuple<ushort, string>)Block.BlockData).Item1);
            // Build the submesh string
            byte[] SubmeshName = Encoding.ASCII.GetBytes(((Tuple<ushort, string>)Block.BlockData).Item2);
            // Write it
            Writer.Write(SubmeshName);
            // Null-term it
            PadStream(Writer, 1);
            // Calculate padding to skip
            int PaddingSize = Convert.ToInt32((StartingPosition + Padded(Writer.BaseStream.Position - StartingPosition)) - Writer.BaseStream.Position);
            // Pad
            PadStream(Writer, PaddingSize);
        }

        private static void XBinBlock_PartInfo_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a part block
            long StartingPosition = Writer.BaseStream.Position;
            // Write ID
            Writer.Write((ushort)Block.BlockID);
            // Write the bone index
            Writer.Write((ushort)((Tuple<ushort, string>)Block.BlockData).Item1);
            // Build the bone name
            byte[] BoneName = Encoding.ASCII.GetBytes(((Tuple<ushort, string>)Block.BlockData).Item2);
            // Write it
            Writer.Write(BoneName);
            // Null-term it
            PadStream(Writer, 1);
            // Calculate padding to skip
            int PaddingSize = Convert.ToInt32((StartingPosition + Padded(Writer.BaseStream.Position - StartingPosition)) - Writer.BaseStream.Position);
            // Pad
            PadStream(Writer, PaddingSize);
        }

        private static void XBinBlock_FrameInfo_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a frame block
            long StartingPosition = Writer.BaseStream.Position;
            // Write ID
            Writer.Write((ushort)Block.BlockID);
            // Pad 2 bytes
            PadStream(Writer, 2);
            // Write the frame index
            Writer.Write((int)((Tuple<int, string>)Block.BlockData).Item1);
            // Build the notetrack name
            byte[] NoteName = Encoding.ASCII.GetBytes(((Tuple<int, string>)Block.BlockData).Item2);
            // Write it
            Writer.Write(NoteName);
            // Null-term it
            PadStream(Writer, 1);
            // Calculate padding to skip
            int PaddingSize = Convert.ToInt32((StartingPosition + Padded(Writer.BaseStream.Position - StartingPosition)) - Writer.BaseStream.Position);
            // Pad
            PadStream(Writer, PaddingSize);
        }

        private static void XBinBlock_BoneInfo_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a comment block
            long StartingPosition = Writer.BaseStream.Position;
            // Write ID
            Writer.Write((ushort)Block.BlockID);
            // Pad two bytes
            PadStream(Writer, 2);
            // Write index and parent
            Writer.Write((int)((Tuple<int, int, string>)Block.BlockData).Item1);
            Writer.Write((int)((Tuple<int, int, string>)Block.BlockData).Item2);
            // Build the name string
            byte[] BoneName = Encoding.ASCII.GetBytes((string)((Tuple<int, int, string>)Block.BlockData).Item3);
            // Write it
            Writer.Write(BoneName);
            // Null-term it
            PadStream(Writer, 1);
            // Calculate padding to skip
            int PaddingSize = Convert.ToInt32((StartingPosition + Padded(Writer.BaseStream.Position - StartingPosition)) - Writer.BaseStream.Position);
            // Pad
            PadStream(Writer, PaddingSize);
        }

        private static void XBinBlock_Vec3_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a vec3 block
            long StartingPosition = Writer.BaseStream.Position;
            // Write ID
            Writer.Write((ushort)Block.BlockID);
            // Pad two bytes
            PadStream(Writer, 2);
            // Write XYZ
            Writer.Write((float)((Tuple<float, float, float>)Block.BlockData).Item1);
            Writer.Write((float)((Tuple<float, float, float>)Block.BlockData).Item2);
            Writer.Write((float)((Tuple<float, float, float>)Block.BlockData).Item3);
            // Calculate padding to skip
            int PaddingSize = Convert.ToInt32((StartingPosition + Padded(Writer.BaseStream.Position - StartingPosition)) - Writer.BaseStream.Position);
            // Pad
            PadStream(Writer, PaddingSize);
        }

        private static void XBinBlock_Vec4_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a vec4 block
            long StartingPosition = Writer.BaseStream.Position;
            // Write ID
            Writer.Write((ushort)Block.BlockID);
            // Pad two bytes
            PadStream(Writer, 2);
            // Write XYZW
            Writer.Write((float)((Tuple<float, float, float, float>)Block.BlockData).Item1);
            Writer.Write((float)((Tuple<float, float, float, float>)Block.BlockData).Item2);
            Writer.Write((float)((Tuple<float, float, float, float>)Block.BlockData).Item3);
            Writer.Write((float)((Tuple<float, float, float, float>)Block.BlockData).Item4);
            // Calculate padding to skip
            int PaddingSize = Convert.ToInt32((StartingPosition + Padded(Writer.BaseStream.Position - StartingPosition)) - Writer.BaseStream.Position);
            // Pad
            PadStream(Writer, PaddingSize);
        }

        private static void XBinBlock_Vec2_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a vec2 block
            long StartingPosition = Writer.BaseStream.Position;
            // Write ID
            Writer.Write((ushort)Block.BlockID);
            // Pad two bytes
            PadStream(Writer, 2);
            // Write XY
            Writer.Write((float)((Tuple<float, float>)Block.BlockData).Item1);
            Writer.Write((float)((Tuple<float, float>)Block.BlockData).Item2);
            // Calculate padding to skip
            int PaddingSize = Convert.ToInt32((StartingPosition + Padded(Writer.BaseStream.Position - StartingPosition)) - Writer.BaseStream.Position);
            // Pad
            PadStream(Writer, PaddingSize);
        }

        private static void XBinBlock_Float_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a float block
            long StartingPosition = Writer.BaseStream.Position;
            // Write ID
            Writer.Write((ushort)Block.BlockID);
            // Pad two bytes
            PadStream(Writer, 2);
            // Write X
            Writer.Write((float)Block.BlockData);
            // Calculate padding to skip
            int PaddingSize = Convert.ToInt32((StartingPosition + Padded(Writer.BaseStream.Position - StartingPosition)) - Writer.BaseStream.Position);
            // Pad
            PadStream(Writer, PaddingSize);
        }

        private static void XBinBlock_ShortVec3_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a short vec3 block
            long StartingPosition = Writer.BaseStream.Position;
            // Write ID
            Writer.Write((ushort)Block.BlockID);
            // Write XYZ as shorts (normalize from -1.0 to 1.0)
            var NormX = (float)Clamp(((Tuple<float, float, float>)Block.BlockData).Item1, -1.0f, 1.0f);
            var NormY = (float)Clamp(((Tuple<float, float, float>)Block.BlockData).Item2, -1.0f, 1.0f);
            var NormZ = (float)Clamp(((Tuple<float, float, float>)Block.BlockData).Item3, -1.0f, 1.0f);
            // Write the data
            Writer.Write((short)(Convert.ToInt16(NormX * 32767.0f)));
            Writer.Write((short)(Convert.ToInt16(NormY * 32767.0f)));
            Writer.Write((short)(Convert.ToInt16(NormZ * 32767.0f)));
            // Calculate padding to skip
            int PaddingSize = Convert.ToInt32((StartingPosition + Padded(Writer.BaseStream.Position - StartingPosition)) - Writer.BaseStream.Position);
            // Pad
            PadStream(Writer, PaddingSize);
        }

        private static void XBinBlock_Quat_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a short vec3 block
            long StartingPosition = Writer.BaseStream.Position;
            // Write ID
            Writer.Write((ushort)Block.BlockID);
            // Write 2 bytes
            Writer.Write((ushort)0x0);
            // Write XYZW as floats
            var NormX = (float)Clamp(((Tuple<float, float, float, float>)Block.BlockData).Item1, -1.0f, 1.0f);
            var NormY = (float)Clamp(((Tuple<float, float, float, float>)Block.BlockData).Item2, -1.0f, 1.0f);
            var NormZ = (float)Clamp(((Tuple<float, float, float, float>)Block.BlockData).Item3, -1.0f, 1.0f);
            var NormW = (float)Clamp(((Tuple<float, float, float, float>)Block.BlockData).Item4, -1.0f, 1.0f);
            // Write the data
            Writer.Write((float)NormX);
            Writer.Write((float)NormY);
            Writer.Write((float)NormZ);
            Writer.Write((float)NormW);
        }

        private static void XBinBlock_VecColor_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a short vec color block
            Writer.Write((ushort)Block.BlockID);
            // Pad it 2 bytes
            PadStream(Writer, 2);
            // Write RGBA as bytes (Normalize from 0.0 to 1.0)
            var NormR = (float)Clamp(((Tuple<float, float, float, float>)Block.BlockData).Item1, 0.0f, 1.0f);
            var NormG = (float)Clamp(((Tuple<float, float, float, float>)Block.BlockData).Item2, 0.0f, 1.0f);
            var NormB = (float)Clamp(((Tuple<float, float, float, float>)Block.BlockData).Item3, 0.0f, 1.0f);
            var NormA = (float)Clamp(((Tuple<float, float, float, float>)Block.BlockData).Item4, 0.0f, 1.0f);
            // Write the data
            Writer.Write((byte)(Convert.ToByte(NormR * 255.0f)));
            Writer.Write((byte)(Convert.ToByte(NormG * 255.0f)));
            Writer.Write((byte)(Convert.ToByte(NormB * 255.0f)));
            Writer.Write((byte)(Convert.ToByte(NormA * 255.0f)));
        }

        private static void XBinBlock_Identifier_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a ID block
            Writer.Write((ushort)Block.BlockID);
            // Pad two bytes
            PadStream(Writer, 2);
        }

        private static void XBinBlock_UInt16_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a int16 block
            Writer.Write((ushort)Block.BlockID);
            // Write data
            Writer.Write((ushort)Block.BlockData);
        }

        private static void XBinBlock_Weight_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a weight block
            Writer.Write((ushort)Block.BlockID);
            // Write boneid
            Writer.Write((ushort)((Tuple<ushort, float>)Block.BlockData).Item1);
            // Write weight value
            Writer.Write((float)((Tuple<ushort, float>)Block.BlockData).Item2);
        }

        private static void XBinBlock_VertUV_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a uv block
            Writer.Write((ushort)Block.BlockID);

            // Get the list
            var UVLayers = (List<Tuple<ushort, float, float>>)Block.BlockData;

            // Write count
            Writer.Write((ushort)UVLayers.Count);

            // Loop and write the layers
            foreach (var Layer in UVLayers)
            {
                // Write the data
                Writer.Write((float)Layer.Item2);
                Writer.Write((float)Layer.Item3);
            }
        }

        private static void XBinBlock_FaceInfo_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a face block
            Writer.Write((ushort)Block.BlockID);
            // Write submesh id
            Writer.Write((byte)((Tuple<byte, byte>)Block.BlockData).Item1);
            // Write material id
            Writer.Write((byte)((Tuple<byte, byte>)Block.BlockData).Item2);
        }

        private static void XBinBlock_FaceInfo16_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a face (16) block
            Writer.Write((ushort)Block.BlockID);
            // Write padding
            Writer.Write((ushort)0x0);
            // Write submesh id
            Writer.Write((ushort)((Tuple<ushort, ushort>)Block.BlockData).Item1);
            // Write material id
            Writer.Write((ushort)((Tuple<ushort, ushort>)Block.BlockData).Item2);
        }

        private static void XBinBlock_Int32_Write(XBlock Block, BinaryWriter Writer)
        {
            // Write a int32 block
            Writer.Write((ushort)Block.BlockID);
            // Pad it 2 bytes
            PadStream(Writer, 2);
            // Write data
            Writer.Write((int)Block.BlockData);
        }

        #endregion

        #region Export Read Handlers

        private static void XExportBlock_Blank_Read(XBlock Block, string CurrentLine, string[] SplitLine)
        {
            // Read nothing (ID is already there...)
        }

        private static void XExportBlock_Comment_Read(XBlock Block, string CurrentLine, string[] SplitLine)
        {
            // Read nothing (Comment is already there...)
            Block.BlockData = CurrentLine.Substring(2).Trim();
        }

        private static void XExportBlock_UInt16_Read(XBlock Block, string CurrentLine, string[] SplitLine)
        {
            // Read a ushort (Handle overflow though) (For NUMVERTS / VERT blocks)
            var Value = Convert.ToInt32(SplitLine[1]);
            // Check
            if (Value > UInt16.MaxValue)
            {
                // We can't cast, check type and convert if we can
                if (Block.TextID == "VERT" || Block.TextID == "NUMVERTS")
                {
                    // We can convert to an integer block
                    var BlockRemap = XExportBlocks[Block.TextID + "32"];
                    // Remap it
                    Block.BinReader = BlockRemap.BinReader;
                    Block.BinWriter = BlockRemap.BinWriter;
                    Block.BlockID = BlockRemap.BlockID;
                    Block.DisplayName = BlockRemap.DisplayName;
                    Block.ExportReader = BlockRemap.ExportReader;
                    Block.ExportWriter = BlockRemap.ExportWriter;
                    Block.TextID = BlockRemap.TextID;
                    // Set the value
                    Block.BlockData = (int)Value;
                }
                else
                {
                    // Just cast to 0
                    Block.BlockData = (UInt16)0;
                }
            }
            else
            {
                // Set it
                Block.BlockData = (UInt16)Value;
            }
        }

        private static void XExportBlock_Int32_Read(XBlock Block, string CurrentLine, string[] SplitLine)
        {
            // Read a int
            Block.BlockData = Convert.ToInt32(SplitLine[1]);
        }

        private static void XExportBlock_Weight_Read(XBlock Block, string CurrentLine, string[] SplitLine)
        {
            // Read a weight
            Block.BlockData = new Tuple<ushort, float>(Convert.ToUInt16(SplitLine[1]), Convert.ToSingle(SplitLine[2]));
        }

        private static void XExportBlock_FaceInfo_Read(XBlock Block, string CurrentLine, string[] SplitLine)
        {
            // Determine if we have a malformed TRI block and adapt if need be
            var SubmeshID = Convert.ToUInt32(SplitLine[1]);
            var MaterialID = Convert.ToUInt32(SplitLine[2]);

            // Swap to TRI16
            if (SubmeshID > byte.MaxValue || MaterialID > byte.MaxValue)
            {
                // We can convert to an integer block
                var BlockRemap = XExportBlocks[Block.TextID + "16"];
                // Remap it
                Block.BinReader = BlockRemap.BinReader;
                Block.BinWriter = BlockRemap.BinWriter;
                Block.BlockID = BlockRemap.BlockID;
                Block.DisplayName = BlockRemap.DisplayName;
                Block.ExportReader = BlockRemap.ExportReader;
                Block.ExportWriter = BlockRemap.ExportWriter;
                Block.TextID = BlockRemap.TextID;
                // Set the value
                Block.BlockData = new Tuple<ushort, ushort>((ushort)SubmeshID, (ushort)MaterialID);
            }
            else
            {
                // Set the face
                Block.BlockData = new Tuple<byte, byte>((byte)SubmeshID, (byte)MaterialID);
            }
        }

        private static void XExportBlock_FaceInfo16_Read(XBlock Block, string CurrentLine, string[] SplitLine)
        {
            // Read a face
            Block.BlockData = new Tuple<ushort, ushort>(Convert.ToUInt16(SplitLine[1]), Convert.ToUInt16(SplitLine[2]));
        }

        private static void XExportBlock_SubmeshInfo_Read(XBlock Block, string CurrentLine, string[] SplitLine)
        {
            // Read a submesh
            Block.BlockData = new Tuple<ushort, string>(Convert.ToUInt16(SplitLine[1]), SplitLine[2].Replace("\"", "").Trim());
        }

        private static void XExportBlock_Frame_Read(XBlock Block, string CurrentLine, string[] SplitLine)
        {
            // Read a frame, be prepared to remove the additional spacing
            string FullNoteStr = string.Join(" ", SplitLine.Skip(2));
            // This should be "notetrack"
            var Start = FullNoteStr.IndexOf("\"");
            var End = FullNoteStr.LastIndexOf("\"");

            // Trim out the name
            FullNoteStr = FullNoteStr.Substring(Start + 1, (End - Start) - 1).Trim();
            // Trim out quotes
            FullNoteStr = FullNoteStr.Replace("\\\"", "\"");

            // Be prepared to process the block
            Block.BlockData = new Tuple<int, string>(Convert.ToInt32(SplitLine[1]), FullNoteStr);
        }

        private static void XExportBlock_BoneInfo_Read(XBlock Block, string CurrentLine, string[] SplitLine)
        {
            // Read bone info
            Block.BlockData = new Tuple<int, int, string>(Convert.ToInt32(SplitLine[1]), Convert.ToInt32(SplitLine[2]), SplitLine[3].Replace("\"", "").Trim());
        }

        private static void XExportBlock_Vec3_Read(XBlock Block, string CurrentLine, string[] SplitLine)
        {
            // Read vec3
            Block.BlockData = new Tuple<float, float, float>(Convert.ToSingle(SplitLine[1]), Convert.ToSingle(SplitLine[2]), Convert.ToSingle(SplitLine[3]));
        }

        private static void XExportBlock_Quat_Read(XBlock Block, string CurrentLine, string[] SplitLine)
        {
            // Read quat
            Block.BlockData = new Tuple<float, float, float, float>(Convert.ToSingle(SplitLine[1]), Convert.ToSingle(SplitLine[2]), Convert.ToSingle(SplitLine[3]), Convert.ToSingle(SplitLine[4]));
        }

        private static void XExportBlock_UV_Read(XBlock Block, string CurrentLine, string[] SplitLine)
        {
            // Read uv
            var UVLayers = new List<Tuple<ushort, float, float>>() { new Tuple<ushort, float, float>(Convert.ToUInt16(SplitLine[1]), Convert.ToSingle(SplitLine[2]), Convert.ToSingle(SplitLine[3])) };
            // Add the layer
            Block.BlockData = UVLayers;
        }

        private static void XExportBlock_MaterialInfo_Read(XBlock Block, string CurrentLine, string[] SplitLine)
        {
            // Read material
            Block.BlockData = new Tuple<ushort, string, string, string>(Convert.ToUInt16(SplitLine[1]), SplitLine[2].Replace("\"", "").Trim(), SplitLine[3].Replace("\"", "").Trim(), SplitLine[4].Replace("\"", "").Trim());
        }

        private static void XExportBlock_Vec4_Read(XBlock Block, string CurrentLine, string[] SplitLine)
        {
            // Read vec4
            Block.BlockData = new Tuple<float, float, float, float>(Convert.ToSingle(SplitLine[1]), Convert.ToSingle(SplitLine[2]), Convert.ToSingle(SplitLine[3]), Convert.ToSingle(SplitLine[4]));
        }

        private static void XExportBlock_Vec2_Read(XBlock Block, string CurrentLine, string[] SplitLine)
        {
            // Read vec2
            Block.BlockData = new Tuple<float, float>(Convert.ToSingle(SplitLine[1]), Convert.ToSingle(SplitLine[2]));
        }

        private static void XExportBlock_Float_Read(XBlock Block, string CurrentLine, string[] SplitLine)
        {
            // Read float
            Block.BlockData = Convert.ToSingle(SplitLine[1]);
        }

        #endregion

        #region Export Write Handlers

        private static void XExportBlock_Comment_Write(XBlock Block, StreamWriter Writer)
        {
            // Write comment
            Writer.WriteLine("// " + (string)Block.BlockData);
        }

        private static void XExportBlock_Identifier_Write(XBlock Block, StreamWriter Writer)
        {
            // Write ID
            Writer.WriteLine(Block.TextID);
        }

        private static void XExportBlock_Version_Write(XBlock Block, StreamWriter Writer)
        {
            // Write version
            Writer.WriteLine(Block.TextID + " " + (ushort)Block.BlockData + Environment.NewLine);
        }

        private static void XExportBlock_Uint16_Write(XBlock Block, StreamWriter Writer)
        {
            // Write count
            Writer.WriteLine(Block.TextID + " " + (ushort)Block.BlockData);
        }

        private static void XExportBlock_Material_Write(XBlock Block, StreamWriter Writer)
        {
            // Write material
            Writer.WriteLine(Block.TextID + " " + ((Tuple<ushort, string, string, string>)Block.BlockData).Item1 + " \"" + ((Tuple<ushort, string, string, string>)Block.BlockData).Item2 + "\"" + " \"" + ((Tuple<ushort, string, string, string>)Block.BlockData).Item3 + "\"" + " \"" + ((Tuple<ushort, string, string, string>)Block.BlockData).Item4 + "\"");
        }

        private static void XExportBlock_Submesh_Write(XBlock Block, StreamWriter Writer)
        {
            // Write count
            Writer.WriteLine(Block.TextID + " " + ((Tuple<ushort, string>)Block.BlockData).Item1 + " \"" + ((Tuple<ushort, string>)Block.BlockData).Item2 + "\"");
        }

        private static void XExportBlock_Int32_Write(XBlock Block, StreamWriter Writer)
        {
            // Write count
            Writer.WriteLine(Block.TextID + " " + (int)Block.BlockData);
        }

        private static void XExportBlock_FaceCount_Write(XBlock Block, StreamWriter Writer)
        {
            // Spacer
            Writer.WriteLine("");
            // Write count
            Writer.WriteLine(Block.TextID + " " + (int)Block.BlockData);
        }

        private static void XExportBlock_FaceInfo_Write(XBlock Block, StreamWriter Writer)
        {
            // Write count
            Writer.WriteLine(Block.TextID + " " + ((Tuple<byte, byte>)Block.BlockData).Item1 + " " + ((Tuple<byte, byte>)Block.BlockData).Item2 + " 0 0");
        }

        private static void XExportBlock_FaceInfo16_Write(XBlock Block, StreamWriter Writer)
        {
            // Write count
            Writer.WriteLine(Block.TextID + " " + ((Tuple<ushort, ushort>)Block.BlockData).Item1 + " " + ((Tuple<ushort, ushort>)Block.BlockData).Item2 + " 0 0");
        }

        private static void XExportBlock_BoneIndex_Write(XBlock Block, StreamWriter Writer)
        {
            // Check
            if ((ushort)Block.BlockData <= 0)
            {
                // Spacer
                Writer.WriteLine("");
            }
            // Write index
            Writer.WriteLine(Block.TextID + " " + (ushort)Block.BlockData);
        }

        private static void XExportBlock_BoneInfo_Write(XBlock Block, StreamWriter Writer)
        {
            // Write info
            Writer.WriteLine(Block.TextID + " " + ((Tuple<int, int, string>)Block.BlockData).Item1 + " " + ((Tuple<int, int, string>)Block.BlockData).Item2 + " \"" + ((Tuple<int, int, string>)Block.BlockData).Item3 + "\"");
        }

        private static void XExportBlock_Weight_Write(XBlock Block, StreamWriter Writer)
        {
            // Write info
            Writer.WriteLine(Block.TextID + " " + ((Tuple<ushort, float>)Block.BlockData).Item1 + " " + ((Tuple<ushort, float>)Block.BlockData).Item2.ToString("0.000000"));
        }

        private static void XExportBlock_UV_Write(XBlock Block, StreamWriter Writer)
        {
            // Write info
            var UVLayers = (List<Tuple<ushort, float, float>>)Block.BlockData;

            // Loop and serialize
            foreach (var Layer in UVLayers)
            {
                Writer.WriteLine(Block.TextID + " " + Layer.Item1 + " " + Layer.Item2.ToString("0.000000") + " " + Layer.Item3.ToString("0.000000"));
            }
        }

        private static void XExportBlock_Vec3_Write(XBlock Block, StreamWriter Writer)
        {
            // Write vec3
            Writer.WriteLine(Block.TextID + " " + ((Tuple<float, float, float>)Block.BlockData).Item1.ToString("0.000000") + ", " + ((Tuple<float, float, float>)Block.BlockData).Item2.ToString("0.000000") + ", " + ((Tuple<float, float, float>)Block.BlockData).Item3.ToString("0.000000"));
            // Check if we have the last matrix entry
            if (Block.TextID == "Z")
            {
                // Spacer
                Writer.WriteLine("");
            }
        }

        private static void XExportBlock_Quat_Write(XBlock Block, StreamWriter Writer)
        {
            // Write quat
            Writer.WriteLine(Block.TextID + " " + ((Tuple<float, float, float, float>)Block.BlockData).Item1.ToString("0.000000") + ", " + ((Tuple<float, float, float, float>)Block.BlockData).Item2.ToString("0.000000") + ", " + ((Tuple<float, float, float, float>)Block.BlockData).Item3.ToString("0.000000") + ", " + ((Tuple<float, float, float, float>)Block.BlockData).Item4.ToString("0.000000"));

            // Check if we have the last streamd data entry
            if (Block.TextID == "QUATERNION")
            {
                // Spacer
                Writer.WriteLine("");
            }
        }

        private static void XExportBlock_PartInfo_Write(XBlock Block, StreamWriter Writer)
        {
            // Write part
            Writer.WriteLine(Block.TextID + " " + ((Tuple<ushort, string>)Block.BlockData).Item1 + " \"" + ((Tuple<ushort, string>)Block.BlockData).Item2 + "\"");
        }

        private static void XExportBlock_Float_Write(XBlock Block, StreamWriter Writer)
        {
            // Write float
            Writer.WriteLine(Block.TextID + " " + ((float)Block.BlockData).ToString("0.000000"));
        }

        private static void XExportBlock_ReflRafl_Write(XBlock Block, StreamWriter Writer)
        {
            // Write vec2
            Writer.WriteLine(Block.TextID + " " + (int)((Tuple<float, float>)Block.BlockData).Item1 + " " + ((Tuple<float, float>)Block.BlockData).Item2.ToString("0.000000"));
        }

        private static void XExportBlock_Glow_Write(XBlock Block, StreamWriter Writer)
        {
            // Write vec2
            Writer.WriteLine(Block.TextID + " " + ((Tuple<float, float>)Block.BlockData).Item1.ToString("0.000000") + " " + (int)((Tuple<float, float>)Block.BlockData).Item2);
        }

        private static void XExportBlock_Frame_Write(XBlock Block, StreamWriter Writer)
        {
            // Write frame
            Writer.WriteLine(Block.TextID + " " + ((Tuple<int, string>)Block.BlockData).Item1 + " \"" + ((Tuple<int, string>)Block.BlockData).Item2.Replace("\"", "\\\"") + "\"");
        }

        private static void XExportBlock_Vec2Plain_Write(XBlock Block, StreamWriter Writer)
        {
            // Write vec2
            Writer.WriteLine(Block.TextID + " " + ((Tuple<float, float>)Block.BlockData).Item1 + " " + ((Tuple<float, float>)Block.BlockData).Item2.ToString("0.000000"));
        }

        private static void XExportBlock_Vec3Plain_Write(XBlock Block, StreamWriter Writer)
        {
            // Write vec3
            Writer.WriteLine(Block.TextID + " " + ((Tuple<float, float, float>)Block.BlockData).Item1.ToString("0.000000") + " " + ((Tuple<float, float, float>)Block.BlockData).Item2.ToString("0.000000") + " " + ((Tuple<float, float, float>)Block.BlockData).Item3.ToString("0.000000"));
        }

        private static void XExportBlock_Vec4Plain_Write(XBlock Block, StreamWriter Writer)
        {
            // Write vec4
            Writer.WriteLine(Block.TextID + " " + ((Tuple<float, float, float, float>)Block.BlockData).Item1.ToString("0.000000") + " " + ((Tuple<float, float, float, float>)Block.BlockData).Item2.ToString("0.000000") + " " + ((Tuple<float, float, float, float>)Block.BlockData).Item3.ToString("0.000000") + " " + ((Tuple<float, float, float, float>)Block.BlockData).Item4.ToString("0.000000"));
        }

        #endregion

        #region Bin Blocks

        // A collection of blocks for bin formats
        private static Dictionary<ushort, XBlock> XBinBlocks = new Dictionary<ushort, XBlock>()
        {
            // Generic entries
            { 0xC355, new XBlock() { BlockID = 0xC355, DisplayName = "Comment block", TextID = "//", BinReader = XBinBlock_Comment_Read, BinWriter = XBinBlock_Comment_Write, ExportReader = XExportBlock_Comment_Read, ExportWriter = XExportBlock_Comment_Write} },
            { 0x46C8, new XBlock() { BlockID = 0x46C8, DisplayName = "Model ID block", TextID = "MODEL", BinReader = XBinBlock_Identifier_Read, BinWriter = XBinBlock_Identifier_Write, ExportReader = XExportBlock_Blank_Read, ExportWriter = XExportBlock_Identifier_Write} },
            { 0x7AAC, new XBlock() { BlockID = 0x7AAC, DisplayName = "Animation ID block", TextID = "ANIMATION", BinReader = XBinBlock_Identifier_Read, BinWriter = XBinBlock_Identifier_Write, ExportReader = XExportBlock_Blank_Read, ExportWriter = XExportBlock_Identifier_Write} },
            { 0xC7F3, new XBlock() { BlockID = 0xC7F3, DisplayName = "Notetrack ID block", TextID = "NOTETRACKS", BinReader = XBinBlock_Identifier_Read, BinWriter = XBinBlock_Identifier_Write, ExportReader = XExportBlock_Blank_Read, ExportWriter = XExportBlock_Identifier_Write} },
            { 0x24D1, new XBlock() { BlockID = 0x24D1, DisplayName = "Version block", TextID = "VERSION", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Version_Write} },

            // Bone specific entries
            { 0x76BA, new XBlock() { BlockID = 0x76BA, DisplayName = "Bone count block", TextID = "NUMBONES", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { 0x1FC2, new XBlock() { BlockID = 0x1FC2, DisplayName = "Streamed bone count block", TextID = "NUMSBONES", BinReader = XBinBlock_Int32_Read, BinWriter = XBinBlock_Int32_Write, ExportReader = XExportBlock_Int32_Read, ExportWriter = XExportBlock_Int32_Write} },
            { 0xB35E, new XBlock() { BlockID = 0xB35E, DisplayName = "Streamed weight count block", TextID = "NUMSWEIGHTS", BinReader = XBinBlock_Int32_Read, BinWriter = XBinBlock_Int32_Write, ExportReader = XExportBlock_Int32_Read, ExportWriter = XExportBlock_Int32_Write} },
            { 0x7836, new XBlock() { BlockID = 0x7836, DisplayName = "Cosmetic bone count block", TextID = "NUMCOSMETICS", BinReader = XBinBlock_Int32_Read, BinWriter = XBinBlock_Int32_Write, ExportReader = XExportBlock_Int32_Read, ExportWriter = XExportBlock_Int32_Write} },
            { 0xF099, new XBlock() { BlockID = 0xF099, DisplayName = "Bone info block", TextID = "BONE", BinReader = XBinBlock_BoneInfo_Read, BinWriter = XBinBlock_BoneInfo_Write, ExportReader = XExportBlock_BoneInfo_Read, ExportWriter = XExportBlock_BoneInfo_Write} },
            { 0xDD9A, new XBlock() { BlockID = 0xDD9A, DisplayName = "Bone index block", TextID = "BONE", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_BoneIndex_Write} },
            { 0x9383, new XBlock() { BlockID = 0x9383, DisplayName = "Vert / Bone offset block", TextID = "OFFSET", BinReader = XBinBlock_Vec3_Read, BinWriter = XBinBlock_Vec3_Write, ExportReader = XExportBlock_Vec3_Read, ExportWriter = XExportBlock_Vec3_Write} },
            { 0x1C56, new XBlock() { BlockID = 0x1C56, DisplayName = "Bone scale block", TextID = "SCALE", BinReader = XBinBlock_Vec3_Read, BinWriter = XBinBlock_Vec3_Write, ExportReader = XExportBlock_Vec3_Read, ExportWriter = XExportBlock_Vec3_Write} },
            { 0xDCFD, new XBlock() { BlockID = 0xDCFD, DisplayName = "Bone x matrix block", TextID = "X", BinReader = XBinBlock_ShortVec3_Read, BinWriter = XBinBlock_ShortVec3_Write, ExportReader = XExportBlock_Vec3_Read, ExportWriter = XExportBlock_Vec3_Write} },
            { 0xCCDC, new XBlock() { BlockID = 0xCCDC, DisplayName = "Bone y matrix block", TextID = "Y", BinReader = XBinBlock_ShortVec3_Read, BinWriter = XBinBlock_ShortVec3_Write, ExportReader = XExportBlock_Vec3_Read, ExportWriter = XExportBlock_Vec3_Write} },
            { 0xFCBF, new XBlock() { BlockID = 0xFCBF, DisplayName = "Bone z matrix block", TextID = "Z", BinReader = XBinBlock_ShortVec3_Read, BinWriter = XBinBlock_ShortVec3_Write, ExportReader = XExportBlock_Vec3_Read, ExportWriter = XExportBlock_Vec3_Write} },
            { 0xEF69, new XBlock() { BlockID = 0xEF69, DisplayName = "Bone quaternion block", TextID = "QUATERNION", BinReader = XBinBlock_Quat_Read, BinWriter = XBinBlock_Quat_Write, ExportReader = XExportBlock_Quat_Read, ExportWriter = XExportBlock_Quat_Write} },

            // Vert specific entries
            { 0x950D, new XBlock() { BlockID = 0x950D, DisplayName = "Number of verts block", TextID = "NUMVERTS", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { 0x2AEC, new XBlock() { BlockID = 0x2AEC, DisplayName = "Number of verts (32) block", TextID = "NUMVERTS32", BinReader = XBinBlock_Int32_Read, BinWriter = XBinBlock_Int32_Write, ExportReader = XExportBlock_Int32_Read, ExportWriter = XExportBlock_Int32_Write} },
            { 0x8F03, new XBlock() { BlockID = 0x8F03, DisplayName = "Vert info block", TextID = "VERT", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { 0xB097, new XBlock() { BlockID = 0xB097, DisplayName = "Vert info (32) block", TextID = "VERT32", BinReader = XBinBlock_Int32_Read, BinWriter = XBinBlock_Int32_Write, ExportReader = XExportBlock_Int32_Read, ExportWriter = XExportBlock_Int32_Write} },
            { 0xEA46, new XBlock() { BlockID = 0xEA46, DisplayName = "Vert weight count block", TextID = "BONES", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { 0xF1AB, new XBlock() { BlockID = 0xF1AB, DisplayName = "Vert bone weight block", TextID = "BONE", BinReader = XBinBlock_Weight_Read, BinWriter = XBinBlock_Weight_Write, ExportReader = XExportBlock_Weight_Read, ExportWriter = XExportBlock_Weight_Write} },

            // Face specific entries
            { 0xBE92, new XBlock() { BlockID = 0xBE92, DisplayName = "Number of faces block", TextID = "NUMFACES", BinReader = XBinBlock_Int32_Read, BinWriter = XBinBlock_Int32_Write, ExportReader = XExportBlock_Int32_Read, ExportWriter = XExportBlock_FaceCount_Write} },
            { 0x562F, new XBlock() { BlockID = 0x562F, DisplayName = "Face info block", TextID = "TRI", BinReader = XBinBlock_FaceInfo_Read, BinWriter = XBinBlock_FaceInfo_Write, ExportReader = XExportBlock_FaceInfo_Read, ExportWriter = XExportBlock_FaceInfo_Write} },
            { 0x6711, new XBlock() { BlockID = 0x6711, DisplayName = "Face info (16) block", TextID = "TRI16", BinReader = XBinBlock_FaceInfo16_Read, BinWriter = XBinBlock_FaceInfo16_Write, ExportReader = XExportBlock_FaceInfo16_Read, ExportWriter = XExportBlock_FaceInfo16_Write} },
            { 0x89EC, new XBlock() { BlockID = 0x89EC, DisplayName = "Normal info block", TextID = "NORMAL", BinReader = XBinBlock_ShortVec3_Read, BinWriter = XBinBlock_ShortVec3_Write, ExportReader = XExportBlock_Vec3_Read, ExportWriter = XExportBlock_Vec3Plain_Write} },
            { 0x6DD8, new XBlock() { BlockID = 0x6DD8, DisplayName = "Color info block", TextID = "COLOR", BinReader = XBinBlock_VertColor_Read, BinWriter = XBinBlock_VecColor_Write, ExportReader = XExportBlock_Vec4_Read, ExportWriter = XExportBlock_Vec4Plain_Write} },
            { 0x1AD4, new XBlock() { BlockID = 0x1AD4, DisplayName = "UV info block", TextID = "UV", BinReader = XBinBlock_VertUV_Read, BinWriter = XBinBlock_VertUV_Write, ExportReader = XExportBlock_UV_Read, ExportWriter = XExportBlock_UV_Write} },

            // Submesh specific entries
            { 0x62AF, new XBlock() { BlockID = 0x62AF, DisplayName = "Number of submeshes block", TextID = "NUMOBJECTS", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { 0x87D4, new XBlock() { BlockID = 0x87D4, DisplayName = "Submesh info block", TextID = "OBJECT", BinReader = XBinBlock_SubmeshInfo_Read, BinWriter = XBinBlock_SubmeshInfo_Write, ExportReader = XExportBlock_SubmeshInfo_Read, ExportWriter = XExportBlock_Submesh_Write} },

            // Material specific entries
            { 0xA1B2, new XBlock() { BlockID = 0xA1B2, DisplayName = "Number of materials block", TextID = "NUMMATERIALS", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { 0xA700, new XBlock() { BlockID = 0xA700, DisplayName = "Material info block", TextID = "MATERIAL", BinReader = XBinBlock_MaterialInfo_Read, BinWriter = XBinBlock_MaterialInfo_Write, ExportReader = XExportBlock_MaterialInfo_Read, ExportWriter = XExportBlock_Material_Write} },
            { 0x6DAB, new XBlock() { BlockID = 0x6DAB, DisplayName = "Material transparency block", TextID = "TRANSPARENCY", BinReader = XBinBlock_Vec4_Read, BinWriter = XBinBlock_Vec4_Write, ExportReader = XExportBlock_Vec4_Read, ExportWriter = XExportBlock_Vec4Plain_Write} },
            { 0x37FF, new XBlock() { BlockID = 0x37FF, DisplayName = "Material color block", TextID = "AMBIENTCOLOR", BinReader = XBinBlock_Vec4_Read, BinWriter = XBinBlock_Vec4_Write, ExportReader = XExportBlock_Vec4_Read, ExportWriter = XExportBlock_Vec4Plain_Write} },
            { 0x4265, new XBlock() { BlockID = 0x4265, DisplayName = "Material incandesence block", TextID = "INCANDESCENCE", BinReader = XBinBlock_Vec4_Read, BinWriter = XBinBlock_Vec4_Write, ExportReader = XExportBlock_Vec4_Read, ExportWriter = XExportBlock_Vec4Plain_Write} },
            { 0xC835, new XBlock() { BlockID = 0xC835, DisplayName = "Material coeffs block", TextID = "COEFFS", BinReader = XBinBlock_Vec2_Read, BinWriter = XBinBlock_Vec2_Write, ExportReader = XExportBlock_Vec2_Read, ExportWriter = XExportBlock_Vec2Plain_Write} },
            { 0xFE0C, new XBlock() { BlockID = 0xFE0C, DisplayName = "Material glow block", TextID = "GLOW", BinReader = XBinBlock_Vec2_Read, BinWriter = XBinBlock_Vec2_Write, ExportReader = XExportBlock_Vec2_Read, ExportWriter = XExportBlock_Glow_Write} },
            { 0x7E24, new XBlock() { BlockID = 0x7E24, DisplayName = "Material refraction block", TextID = "REFRACTIVE", BinReader = XBinBlock_Vec2_Read, BinWriter = XBinBlock_Vec2_Write, ExportReader = XExportBlock_Vec2_Read, ExportWriter = XExportBlock_ReflRafl_Write} },
            { 0x317C, new XBlock() { BlockID = 0x317C, DisplayName = "Material specular color block", TextID = "SPECULARCOLOR", BinReader = XBinBlock_Vec4_Read, BinWriter = XBinBlock_Vec4_Write, ExportReader = XExportBlock_Vec4_Read, ExportWriter = XExportBlock_Vec4Plain_Write} },
            { 0xE593, new XBlock() { BlockID = 0xE593, DisplayName = "Material reflective color block", TextID = "REFLECTIVECOLOR", BinReader = XBinBlock_Vec4_Read, BinWriter = XBinBlock_Vec4_Write, ExportReader = XExportBlock_Vec4_Read, ExportWriter = XExportBlock_Vec4Plain_Write} },
            { 0x7D76, new XBlock() { BlockID = 0x7D76, DisplayName = "Material reflective block", TextID = "REFLECTIVE", BinReader = XBinBlock_Vec2_Read, BinWriter = XBinBlock_Vec2_Write, ExportReader = XExportBlock_Vec2_Read, ExportWriter = XExportBlock_ReflRafl_Write} },
            { 0x83C7, new XBlock() { BlockID = 0x83C7, DisplayName = "Material blinn block", TextID = "BLINN", BinReader = XBinBlock_Vec2_Read, BinWriter = XBinBlock_Vec2_Write, ExportReader = XExportBlock_Vec2_Read, ExportWriter = XExportBlock_Vec2Plain_Write} },
            { 0x5CD2, new XBlock() { BlockID = 0x5CD2, DisplayName = "Material phong block", TextID = "PHONG", BinReader = XBinBlock_Float_Read, BinWriter = XBinBlock_Float_Write, ExportReader = XExportBlock_Float_Read, ExportWriter = XExportBlock_Float_Write} },

            // Anim specific entries
            { 0x9279, new XBlock() { BlockID = 0x9279, DisplayName = "Number of parts block", TextID = "NUMPARTS", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { 0x360B, new XBlock() { BlockID = 0x360B, DisplayName = "Part info block", TextID = "PART", BinReader = XBinBlock_PartInfo_Read, BinWriter = XBinBlock_PartInfo_Write, ExportReader = XExportBlock_SubmeshInfo_Read, ExportWriter = XExportBlock_PartInfo_Write} },
            { 0x92D3, new XBlock() { BlockID = 0x92D3, DisplayName = "Framerate block", TextID = "FRAMERATE", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { 0xB917, new XBlock() { BlockID = 0xB917, DisplayName = "Number of frames block", TextID = "NUMFRAMES", BinReader = XBinBlock_Int32_Read, BinWriter = XBinBlock_Int32_Write, ExportReader = XExportBlock_Int32_Read, ExportWriter = XExportBlock_Int32_Write} },
            { 0xC723, new XBlock() { BlockID = 0xC723, DisplayName = "Frame block", TextID = "FRAME", BinReader = XBinBlock_Int32_Read, BinWriter = XBinBlock_Int32_Write, ExportReader = XExportBlock_Int32_Read, ExportWriter = XExportBlock_Int32_Write} },
            { 0x745A, new XBlock() { BlockID = 0x745A, DisplayName = "Part block", TextID = "PART", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read,  ExportWriter = XExportBlock_Uint16_Write} },
            { 0x7A6C, new XBlock() { BlockID = 0x7A6C, DisplayName = "Number of keys block", TextID = "NUMKEYS", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { 0x9016, new XBlock() { BlockID = 0x9016, DisplayName = "Number of notetracks block", TextID = "NUMTRACKS", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { 0x4643, new XBlock() { BlockID = 0x4643, DisplayName = "Notetrack block", TextID = "NOTETRACK", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { 0x1675, new XBlock() { BlockID = 0x1675, DisplayName = "Notetrack frame block", TextID = "FRAME", BinReader = XBinBlock_FrameInfo_Read, BinWriter = XBinBlock_FrameInfo_Write, ExportReader = XExportBlock_Frame_Read, ExportWriter = XExportBlock_Frame_Write} },
        };

        #endregion

        #region Export Blocks

        // A collection of blocks for text formats
        private static Dictionary<string, XBlock> XExportBlocks = new Dictionary<string, XBlock>()
        {
            // Generic entries
            { "//", new XBlock() { BlockID = 0xC355, DisplayName = "Comment block", TextID = "//", BinReader = XBinBlock_Comment_Read, BinWriter = XBinBlock_Comment_Write, ExportReader = XExportBlock_Comment_Read, ExportWriter = XExportBlock_Comment_Write} },
            { "MODEL", new XBlock() { BlockID = 0x46C8, DisplayName = "Model ID block", TextID = "MODEL", BinReader = XBinBlock_Identifier_Read, BinWriter = XBinBlock_Identifier_Write, ExportReader = XExportBlock_Blank_Read, ExportWriter = XExportBlock_Identifier_Write} },
            { "ANIMATION", new XBlock() { BlockID = 0x7AAC, DisplayName = "Animation ID block", TextID = "ANIMATION", BinReader = XBinBlock_Identifier_Read, BinWriter = XBinBlock_Identifier_Write, ExportReader = XExportBlock_Blank_Read, ExportWriter = XExportBlock_Identifier_Write} },
            { "NOTETRACKS", new XBlock() { BlockID = 0xC7F3, DisplayName = "Notetrack ID block", TextID = "NOTETRACKS", BinReader = XBinBlock_Identifier_Read, BinWriter = XBinBlock_Identifier_Write, ExportReader = XExportBlock_Blank_Read, ExportWriter = XExportBlock_Identifier_Write} },
            { "VERSION", new XBlock() { BlockID = 0x24D1, DisplayName = "Version block", TextID = "VERSION", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Version_Write} },

            // Bone specific entries
            { "NUMBONES", new XBlock() { BlockID = 0x76BA, DisplayName = "Bone count block", TextID = "NUMBONES", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { "NUMSBONES", new XBlock() { BlockID = 0x1FC2, DisplayName = "Streamed bone count block", TextID = "NUMSBONES", BinReader = XBinBlock_Int32_Read, BinWriter = XBinBlock_Int32_Write, ExportReader = XExportBlock_Int32_Read, ExportWriter = XExportBlock_Int32_Write} },
            { "NUMSWEIGHTS", new XBlock() { BlockID = 0xB35E, DisplayName = "Streamed weight count block", TextID = "NUMSWEIGHTS", BinReader = XBinBlock_Int32_Read, BinWriter = XBinBlock_Int32_Write, ExportReader = XExportBlock_Int32_Read, ExportWriter = XExportBlock_Int32_Write} },
            { "NUMCOSMETICS", new XBlock() { BlockID = 0x7836, DisplayName = "Cosmetic bone count block", TextID = "NUMCOSMETICS", BinReader = XBinBlock_Int32_Read, BinWriter = XBinBlock_Int32_Write, ExportReader = XExportBlock_Int32_Read, ExportWriter = XExportBlock_Int32_Write} },
            { "BONE_INFO", new XBlock() { BlockID = 0xF099, DisplayName = "Bone info block", TextID = "BONE", BinReader = XBinBlock_BoneInfo_Read, BinWriter = XBinBlock_BoneInfo_Write, ExportReader = XExportBlock_BoneInfo_Read, ExportWriter = XExportBlock_BoneInfo_Write} },
            { "BONE_INDEX", new XBlock() { BlockID = 0xDD9A, DisplayName = "Bone index block", TextID = "BONE", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_BoneIndex_Write} },
            { "OFFSET", new XBlock() { BlockID = 0x9383, DisplayName = "Vert / Bone offset block", TextID = "OFFSET", BinReader = XBinBlock_Vec3_Read, BinWriter = XBinBlock_Vec3_Write, ExportReader = XExportBlock_Vec3_Read, ExportWriter = XExportBlock_Vec3_Write} },
            { "SCALE", new XBlock() { BlockID = 0x1C56, DisplayName = "Bone scale block", TextID = "SCALE", BinReader = XBinBlock_Vec3_Read, BinWriter = XBinBlock_Vec3_Write, ExportReader = XExportBlock_Vec3_Read, ExportWriter = XExportBlock_Vec3_Write} },
            { "X", new XBlock() { BlockID = 0xDCFD, DisplayName = "Bone x matrix block", TextID = "X", BinReader = XBinBlock_ShortVec3_Read, BinWriter = XBinBlock_ShortVec3_Write, ExportReader = XExportBlock_Vec3_Read, ExportWriter = XExportBlock_Vec3_Write} },
            { "Y", new XBlock() { BlockID = 0xCCDC, DisplayName = "Bone y matrix block", TextID = "Y", BinReader = XBinBlock_ShortVec3_Read, BinWriter = XBinBlock_ShortVec3_Write, ExportReader = XExportBlock_Vec3_Read, ExportWriter = XExportBlock_Vec3_Write} },
            { "Z", new XBlock() { BlockID = 0xFCBF, DisplayName = "Bone z matrix block", TextID = "Z", BinReader = XBinBlock_ShortVec3_Read, BinWriter = XBinBlock_ShortVec3_Write, ExportReader = XExportBlock_Vec3_Read, ExportWriter = XExportBlock_Vec3_Write} },
            { "QUATERNION", new XBlock() { BlockID = 0xEF69, DisplayName = "Bone quaternion block", TextID = "QUATERNION", BinReader = XBinBlock_Quat_Read, BinWriter = XBinBlock_Quat_Write, ExportReader = XExportBlock_Quat_Read, ExportWriter = XExportBlock_Quat_Write} },

            // Vert specific entries
            { "NUMVERTS", new XBlock() { BlockID = 0x950D, DisplayName = "Number of verts block", TextID = "NUMVERTS", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { "NUMVERTS32", new XBlock() { BlockID = 0x2AEC, DisplayName = "Number of verts (32) block", TextID = "NUMVERTS32", BinReader = XBinBlock_Int32_Read, BinWriter = XBinBlock_Int32_Write, ExportReader = XExportBlock_Int32_Read, ExportWriter = XExportBlock_Int32_Write} },
            { "VERT", new XBlock() { BlockID = 0x8F03, DisplayName = "Vert info block", TextID = "VERT", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { "VERT32", new XBlock() { BlockID = 0xB097, DisplayName = "Vert info (32) block", TextID = "VERT32", BinReader = XBinBlock_Int32_Read, BinWriter = XBinBlock_Int32_Write, ExportReader = XExportBlock_Int32_Read, ExportWriter = XExportBlock_Int32_Write} },
            { "BONES", new XBlock() { BlockID = 0xEA46, DisplayName = "Vert weight count block", TextID = "BONES", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { "BONE_WEIGHT", new XBlock() { BlockID = 0xF1AB, DisplayName = "Vert bone weight block", TextID = "BONE", BinReader = XBinBlock_Weight_Read, BinWriter = XBinBlock_Weight_Write, ExportReader = XExportBlock_Weight_Read, ExportWriter = XExportBlock_Weight_Write} },

            // Face specific entries
            { "NUMFACES", new XBlock() { BlockID = 0xBE92, DisplayName = "Number of faces block", TextID = "NUMFACES", BinReader = XBinBlock_Int32_Read, BinWriter = XBinBlock_Int32_Write, ExportReader = XExportBlock_Int32_Read, ExportWriter = XExportBlock_FaceCount_Write} },
            { "TRI", new XBlock() { BlockID = 0x562F, DisplayName = "Face info block", TextID = "TRI", BinReader = XBinBlock_FaceInfo_Read, BinWriter = XBinBlock_FaceInfo_Write, ExportReader = XExportBlock_FaceInfo_Read, ExportWriter = XExportBlock_FaceInfo_Write} },
            { "TRI16", new XBlock() { BlockID = 0x6711, DisplayName = "Face info (16) block", TextID = "TRI16", BinReader = XBinBlock_FaceInfo16_Read, BinWriter = XBinBlock_FaceInfo16_Write, ExportReader = XExportBlock_FaceInfo16_Read, ExportWriter = XExportBlock_FaceInfo16_Write} },
            { "NORMAL", new XBlock() { BlockID = 0x89EC, DisplayName = "Normal info block", TextID = "NORMAL", BinReader = XBinBlock_ShortVec3_Read, BinWriter = XBinBlock_ShortVec3_Write, ExportReader = XExportBlock_Vec3_Read, ExportWriter = XExportBlock_Vec3Plain_Write} },
            { "COLOR", new XBlock() { BlockID = 0x6DD8, DisplayName = "Color info block", TextID = "COLOR", BinReader = XBinBlock_VertColor_Read, BinWriter = XBinBlock_VecColor_Write, ExportReader = XExportBlock_Vec4_Read, ExportWriter = XExportBlock_Vec4Plain_Write} },
            { "UV", new XBlock() { BlockID = 0x1AD4, DisplayName = "UV info block", TextID = "UV", BinReader = XBinBlock_VertUV_Read, BinWriter = XBinBlock_VertUV_Write, ExportReader = XExportBlock_UV_Read, ExportWriter = XExportBlock_UV_Write} },

            // Submesh specific entries
            { "NUMOBJECTS", new XBlock() { BlockID = 0x62AF, DisplayName = "Number of submeshes block", TextID = "NUMOBJECTS", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { "OBJECT", new XBlock() { BlockID = 0x87D4, DisplayName = "Submesh info block", TextID = "OBJECT", BinReader = XBinBlock_SubmeshInfo_Read, BinWriter = XBinBlock_SubmeshInfo_Write, ExportReader = XExportBlock_SubmeshInfo_Read, ExportWriter = XExportBlock_Submesh_Write} },

            // Material specific entries
            { "NUMMATERIALS", new XBlock() { BlockID = 0xA1B2, DisplayName = "Number of materials block", TextID = "NUMMATERIALS", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { "MATERIAL", new XBlock() { BlockID = 0xA700, DisplayName = "Material info block", TextID = "MATERIAL", BinReader = XBinBlock_MaterialInfo_Read, BinWriter = XBinBlock_MaterialInfo_Write, ExportReader = XExportBlock_MaterialInfo_Read, ExportWriter = XExportBlock_Material_Write} },
            { "TRANSPARENCY", new XBlock() { BlockID = 0x6DAB, DisplayName = "Material transparency block", TextID = "TRANSPARENCY", BinReader = XBinBlock_Vec4_Read, BinWriter = XBinBlock_Vec4_Write, ExportReader = XExportBlock_Vec4_Read, ExportWriter = XExportBlock_Vec4Plain_Write} },
            { "AMBIENTCOLOR", new XBlock() { BlockID = 0x37FF, DisplayName = "Material color block", TextID = "AMBIENTCOLOR", BinReader = XBinBlock_Vec4_Read, BinWriter = XBinBlock_Vec4_Write, ExportReader = XExportBlock_Vec4_Read, ExportWriter = XExportBlock_Vec4Plain_Write} },
            { "INCANDESCENCE", new XBlock() { BlockID = 0x4265, DisplayName = "Material incandesence block", TextID = "INCANDESCENCE", BinReader = XBinBlock_Vec4_Read, BinWriter = XBinBlock_Vec4_Write, ExportReader = XExportBlock_Vec4_Read, ExportWriter = XExportBlock_Vec4Plain_Write} },
            { "COEFFS", new XBlock() { BlockID = 0xC835, DisplayName = "Material coeffs block", TextID = "COEFFS", BinReader = XBinBlock_Vec2_Read, BinWriter = XBinBlock_Vec2_Write, ExportReader = XExportBlock_Vec2_Read, ExportWriter = XExportBlock_Vec2Plain_Write} },
            { "GLOW", new XBlock() { BlockID = 0xFE0C, DisplayName = "Material glow block", TextID = "GLOW", BinReader = XBinBlock_Vec2_Read, BinWriter = XBinBlock_Vec2_Write, ExportReader = XExportBlock_Vec2_Read, ExportWriter = XExportBlock_Glow_Write} },
            { "REFRACTIVE", new XBlock() { BlockID = 0x7E24, DisplayName = "Material refraction block", TextID = "REFRACTIVE", BinReader = XBinBlock_Vec2_Read, BinWriter = XBinBlock_Vec2_Write, ExportReader = XExportBlock_Vec2_Read, ExportWriter = XExportBlock_ReflRafl_Write} },
            { "SPECULARCOLOR", new XBlock() { BlockID = 0x317C, DisplayName = "Material specular color block", TextID = "SPECULARCOLOR", BinReader = XBinBlock_Vec4_Read, BinWriter = XBinBlock_Vec4_Write, ExportReader = XExportBlock_Vec4_Read, ExportWriter = XExportBlock_Vec4Plain_Write} },
            { "REFLECTIVECOLOR", new XBlock() { BlockID = 0xE593, DisplayName = "Material reflective color block", TextID = "REFLECTIVECOLOR", BinReader = XBinBlock_Vec4_Read, BinWriter = XBinBlock_Vec4_Write, ExportReader = XExportBlock_Vec4_Read, ExportWriter = XExportBlock_Vec4Plain_Write} },
            { "REFLECTIVE", new XBlock() { BlockID = 0x7D76, DisplayName = "Material reflective block", TextID = "REFLECTIVE", BinReader = XBinBlock_Vec2_Read, BinWriter = XBinBlock_Vec2_Write, ExportReader = XExportBlock_Vec2_Read, ExportWriter = XExportBlock_ReflRafl_Write} },
            { "BLINN", new XBlock() { BlockID = 0x83C7, DisplayName = "Material blinn block", TextID = "BLINN", BinReader = XBinBlock_Vec2_Read, BinWriter = XBinBlock_Vec2_Write, ExportReader = XExportBlock_Vec2_Read, ExportWriter = XExportBlock_Vec2Plain_Write} },
            { "PHONG", new XBlock() { BlockID = 0x5CD2, DisplayName = "Material phong block", TextID = "PHONG", BinReader = XBinBlock_Float_Read, BinWriter = XBinBlock_Float_Write, ExportReader = XExportBlock_Float_Read, ExportWriter = XExportBlock_Float_Write} },

            // Anim specific entries
            { "NUMPARTS", new XBlock() { BlockID = 0x9279, DisplayName = "Number of parts block", TextID = "NUMPARTS", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { "PART_INFO", new XBlock() { BlockID = 0x360B, DisplayName = "Part info block", TextID = "PART", BinReader = XBinBlock_PartInfo_Read, BinWriter = XBinBlock_PartInfo_Write, ExportReader = XExportBlock_SubmeshInfo_Read, ExportWriter = XExportBlock_PartInfo_Write} },
            { "FRAMERATE", new XBlock() { BlockID = 0x92D3, DisplayName = "Framerate block", TextID = "FRAMERATE", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { "NUMFRAMES", new XBlock() { BlockID = 0xB917, DisplayName = "Number of frames block", TextID = "NUMFRAMES", BinReader = XBinBlock_Int32_Read, BinWriter = XBinBlock_Int32_Write, ExportReader = XExportBlock_Int32_Read, ExportWriter = XExportBlock_Int32_Write} },
            { "FRAME_KEY", new XBlock() { BlockID = 0xC723, DisplayName = "Frame block", TextID = "FRAME", BinReader = XBinBlock_Int32_Read, BinWriter = XBinBlock_Int32_Write, ExportReader = XExportBlock_Int32_Read, ExportWriter = XExportBlock_Int32_Write} },
            { "PART_KEY", new XBlock() { BlockID = 0x745A, DisplayName = "Part block", TextID = "PART", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read,  ExportWriter = XExportBlock_Uint16_Write} },
            { "NUMKEYS", new XBlock() { BlockID = 0x7A6C, DisplayName = "Number of keys block", TextID = "NUMKEYS", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { "NUMTRACKS", new XBlock() { BlockID = 0x9016, DisplayName = "Number of notetracks block", TextID = "NUMTRACKS", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { "NOTETRACK", new XBlock() { BlockID = 0x4643, DisplayName = "Notetrack block", TextID = "NOTETRACK", BinReader = XBinBlock_UInt16_Read, BinWriter = XBinBlock_UInt16_Write, ExportReader = XExportBlock_UInt16_Read, ExportWriter = XExportBlock_Uint16_Write} },
            { "FRAME_NOTE", new XBlock() { BlockID = 0x1675, DisplayName = "Notetrack frame block", TextID = "FRAME", BinReader = XBinBlock_FrameInfo_Read, BinWriter = XBinBlock_FrameInfo_Write, ExportReader = XExportBlock_Frame_Read, ExportWriter = XExportBlock_Frame_Write} },
        };

        #endregion

        /// <summary>
        /// Loads a stream with the specified type
        /// </summary>
        /// <param name="Buffer">The data to load from</param>
        /// <param name="Type">The type of data in the file</param>
        public XAssetFile(Stream Buffer, InFormat Type)
        {
            // Setup
            FileBlocks = new List<XBlock>();

            // Load the buffer and parse it
            switch (Type)
            {
                case InFormat.Bin:
                    ProcessXBin(Buffer);
                    break;
                case InFormat.Export:
                    ProcessXExport(Buffer);
                    break;
            }
            
            // Post process
            RunPostProcess();
        }

        /// <summary>
        /// Write the XAsset to an export based file
        /// </summary>
        /// <param name="FilePath">The file name to save to</param>
        public void WriteExport(string FilePath)
        {
            // Save to export format
            using (StreamWriter writeFile = new StreamWriter(File.Create(FilePath)))
            {
                // Loop through blocks and call the writer
                foreach (XBlock fileBlock in FileBlocks)
                {
                    // Invoke it
                    if (fileBlock.ExportWriter != null)
                    {
                        // Go
                        fileBlock.ExportWriter(fileBlock, writeFile);
                    }
                    else
                    {
                        // Unsupported export block
                        Console.WriteLine(":  Unknown export ID [0x" + fileBlock.BlockID.ToString("X2") + ":" + fileBlock.TextID + "]");
                    }
                }
            }
        }

        /// <summary>
        /// Write the XAsset to a bin based file
        /// </summary>
        /// <param name="FilePath">The file name to save to</param>
        public void WriteBin(string FilePath)
        {
            // Save to bin format (A memorystream first)
            using (BinaryWriter writeFile = new BinaryWriter(new MemoryStream()))
            {
                // Loop through blocks and call the writer
                foreach (XBlock fileBlock in FileBlocks)
                {
                    // Invoke it
                    if (fileBlock.BinWriter != null)
                    {
                        // Go
                        fileBlock.BinWriter(fileBlock, writeFile);
                    }
                    else
                    {
                        // Unsupported export block
                        Console.WriteLine(":  Unknown export ID [0x" + fileBlock.BlockID.ToString("X2") + ":" + fileBlock.TextID + "]");
                    }
                }
                // Now save the buffer to file (LZ4 and header) ('*LZ4*' uncompressed size uint)
                writeFile.BaseStream.Position = 0;
                // Decompressed
                byte[] DecompressedBuffer = new byte[writeFile.BaseStream.Length];
                // Read
                writeFile.BaseStream.Read(DecompressedBuffer, 0, DecompressedBuffer.Length);
                // Compress
                byte[] CompressedBuffer = LZ4.LZ4Codec.EncodeHC(DecompressedBuffer, 0, (int)writeFile.BaseStream.Length);
                // Write
                using (BinaryWriter WriteBuffer = new BinaryWriter(File.Create(FilePath)))
                {
                    // Write magic
                    WriteBuffer.Write(new char[] { '*', 'L', 'Z', '4', '*' });
                    // Write uncompressed length
                    WriteBuffer.Write((uint)DecompressedBuffer.Length);
                    // Write compressed data
                    WriteBuffer.Write(CompressedBuffer);
                }
            }
        }

        private void ProcessXExport(Stream FileLoad)
        {
            // Reset error
            ErrorLine = 1;
            Format = InFormat.Export;
            // Load the export file, process each line to blocks
            using (StreamReader ReadFile = new StreamReader(FileLoad))
            {
                // Prepare to read line by line
                while (!ReadFile.EndOfStream)
                {
                    // Get the line
                    string CurrentLine = ReadFile.ReadLine().Trim();
                    // Split
                    string[] SplitLine = (CurrentLine.Contains(",") == true) ? CurrentLine.Split(' ').Select(s => s.Replace(",", "").Trim()).ToArray() : CurrentLine.Split(' ');
                    // Check
                    if (!string.IsNullOrEmpty(CurrentLine) && !string.IsNullOrWhiteSpace(CurrentLine))
                    {
                        // Check for the ID
                        if (SplitLine[0] == "BONE")
                        {
                            // Handle bone types
                            switch (SplitLine.Length)
                            {
                                case 2:
                                    {
                                        // Grab the base block
                                        var BaseBlock = XExportBlocks["BONE_INDEX"];
                                        // Clone it, then allow it to read itself
                                        var ReadBlock = (XBlock)BaseBlock.Clone();
                                        // Check if we can read
                                        if (ReadBlock.ExportReader != null)
                                        {
                                            // Read itself, then add
                                            ReadBlock.ExportReader(ReadBlock, CurrentLine, SplitLine);
                                            // Add
                                            this.FileBlocks.Add(ReadBlock);
                                        }
                                    }
                                    break;
                                case 3:
                                    {
                                        // Grab the base block
                                        var BaseBlock = XExportBlocks["BONE_WEIGHT"];
                                        // Clone it, then allow it to read itself
                                        var ReadBlock = (XBlock)BaseBlock.Clone();
                                        // Check if we can read
                                        if (ReadBlock.ExportReader != null)
                                        {
                                            // Read itself, then add
                                            ReadBlock.ExportReader(ReadBlock, CurrentLine, SplitLine);
                                            // Add
                                            this.FileBlocks.Add(ReadBlock);
                                        }
                                    }
                                    break;
                                case 4:
                                    {
                                        // Grab the base block
                                        var BaseBlock = XExportBlocks["BONE_INFO"];
                                        // Clone it, then allow it to read itself
                                        var ReadBlock = (XBlock)BaseBlock.Clone();
                                        // Check if we can read
                                        if (ReadBlock.ExportReader != null)
                                        {
                                            // Read itself, then add
                                            ReadBlock.ExportReader(ReadBlock, CurrentLine, SplitLine);
                                            // Add
                                            this.FileBlocks.Add(ReadBlock);
                                        }
                                    }
                                    break;
                            }
                        }
                        else if (SplitLine[0] == "PART")
                        {
                            // Handle part types
                            switch (SplitLine.Length)
                            {
                                case 2:
                                    {
                                        // Grab the base block
                                        var BaseBlock = XExportBlocks["PART_KEY"];
                                        // Clone it, then allow it to read itself
                                        var ReadBlock = (XBlock)BaseBlock.Clone();
                                        // Check if we can read
                                        if (ReadBlock.ExportReader != null)
                                        {
                                            // Read itself, then add
                                            ReadBlock.ExportReader(ReadBlock, CurrentLine, SplitLine);
                                            // Add
                                            this.FileBlocks.Add(ReadBlock);
                                        }
                                    }
                                    break;
                                case 3:
                                    {
                                        // Grab the base block
                                        var BaseBlock = XExportBlocks["PART_INFO"];
                                        // Clone it, then allow it to read itself
                                        var ReadBlock = (XBlock)BaseBlock.Clone();
                                        // Check if we can read
                                        if (ReadBlock.ExportReader != null)
                                        {
                                            // Read itself, then add
                                            ReadBlock.ExportReader(ReadBlock, CurrentLine, SplitLine);
                                            // Add
                                            this.FileBlocks.Add(ReadBlock);
                                        }
                                    }
                                    break;
                            }
                        }
                        else if (SplitLine[0] == "FRAME")
                        {
                            // Handle frame types
                            switch (SplitLine.Length)
                            {
                                case 2:
                                    {
                                        // Grab the base block
                                        var BaseBlock = XExportBlocks["FRAME_KEY"];
                                        // Clone it, then allow it to read itself
                                        var ReadBlock = (XBlock)BaseBlock.Clone();
                                        // Check if we can read
                                        if (ReadBlock.ExportReader != null)
                                        {
                                            // Read itself, then add
                                            ReadBlock.ExportReader(ReadBlock, CurrentLine, SplitLine);
                                            // Add
                                            this.FileBlocks.Add(ReadBlock);
                                        }
                                    }
                                    break;
                                default:
                                    {
                                        // Grab the base block
                                        var BaseBlock = XExportBlocks["FRAME_NOTE"];
                                        // Clone it, then allow it to read itself
                                        var ReadBlock = (XBlock)BaseBlock.Clone();
                                        // Check if we can read
                                        if (ReadBlock.ExportReader != null)
                                        {
                                            // Read itself, then add
                                            ReadBlock.ExportReader(ReadBlock, CurrentLine, SplitLine);
                                            // Add
                                            this.FileBlocks.Add(ReadBlock);
                                        }
                                    }
                                    break;
                            }
                        }
                        else
                        {
                            // Check the ID
                            if (XExportBlocks.ContainsKey(SplitLine[0]))
                            {
                                // Grab the base block
                                var BaseBlock = XExportBlocks[SplitLine[0]];
                                // Clone it, then allow it to read itself
                                var ReadBlock = (XBlock)BaseBlock.Clone();
                                // Check if we can read
                                if (ReadBlock.ExportReader != null)
                                {
                                    // Read itself, then add
                                    ReadBlock.ExportReader(ReadBlock, CurrentLine, SplitLine);
                                    // Add
                                    this.FileBlocks.Add(ReadBlock);
                                }
                            }
                            else
                            {
                                // Unknown block ID
                                Console.WriteLine(":  Unknown block ID [" + SplitLine[0] + "]");
                                // Stop
                                break;
                            }
                        }
                    }
                    // Increase
                    ErrorLine++;
                }
            }
            // Collect garbage we loaded
            GC.Collect();
        }

        private void ProcessXBin(Stream FileLoad)
        {
            // Reset error
            ErrorLine = 0;
            Format = InFormat.Bin;
            // Load the bin file, decompress the buffer to memory then handle blocks
            using (BinaryReader readFile = new BinaryReader(FileLoad))
            {
                // Check magic
                char[] Magic = readFile.ReadChars(5);
                // Check
                if (Magic.SequenceEqual(new char[] { '*', 'L', 'Z', '4', '*' }))
                {
                    try
                    {
                        // Load the file
                        uint UncompressedSize = readFile.ReadUInt32();
                        // Read the buffer
                        byte[] CompressedBuffer = readFile.ReadBytes((int)(readFile.BaseStream.Length - readFile.BaseStream.Position));
                        // Decompress to a mem buffer
                        using (BinaryReader ReadBuffer = new BinaryReader(new MemoryStream(LZ4.LZ4Codec.Decode(CompressedBuffer, 0, CompressedBuffer.Length, (int)UncompressedSize))))
                        {
                            // This buffer will serve the blocks
                            ushort Hash = ReadBuffer.ReadUInt16();
                            // Loop until end
                            while (true)
                            {
                                // Read the block if we have it in our list of xblocks
                                if (XBinBlocks.ContainsKey(Hash))
                                {
                                    // Grab the base block
                                    var BaseBlock = XBinBlocks[Hash];
                                    // Clone it, then allow it to read itself
                                    var ReadBlock = (XBlock)BaseBlock.Clone();
                                    // Check if we can
                                    if (ReadBlock.BinReader != null)
                                    {
                                        // Read itself, then add
                                        ReadBlock.BinReader(ReadBlock, ReadBuffer);
                                        // Add
                                        this.FileBlocks.Add(ReadBlock);
                                    }
                                }
                                else
                                {
                                    // Unknown block ID
                                    Console.WriteLine(":  Unknown block ID [0x" + Hash.ToString("X2") + "]");
                                    // Stop
                                    break;
                                }
                                // Check length
                                var CurrentPosition = ReadBuffer.BaseStream.Position;
                                // Read next hash
                                if (CurrentPosition != ReadBuffer.BaseStream.Length)
                                {
                                    // Read next hash
                                    Hash = ReadBuffer.ReadUInt16();
                                }
                                else
                                {
                                    // Done
                                    break;
                                }
                                // Set last
                                ErrorLine = CurrentPosition;
                            }
                        }
                    }
                    catch
                    {
                        // Nothing, just clean up below
                        Console.WriteLine(":  Failed to load bin file (Compression)");
                    }
                    // Collect garbage we loaded
                    GC.Collect();
                }
                else
                {
                    // Bad file
                    Console.WriteLine(":  Invalid bin file magic");
                }
            }
        }

        private void RunPostProcess()
        {
            // Perform post processing on the blocks
            if (this.FileBlocks != null)
            {
                try
                {
                    // Iterate in parallel and process
                    Parallel.ForEach<XBlock>(this.FileBlocks, (Block) =>
                    {
                        // Stage 1: Check normals for errors
                        if (Block.BlockID == 0x89EC)
                        {
                            // Get absolute sum
                            var Values = (Tuple<float, float, float>)Block.BlockData;
                            // Add and check
                            if ((Math.Abs(Values.Item1) + Math.Abs(Values.Item2) + Math.Abs(Values.Item3)) == 0.0f)
                            {
                                // Reset to 0.0 0.0 1.0
                                Block.BlockData = new Tuple<float, float, float>(0.0f, 0.0f, 1.0f);
                            }
                        }
                    });
                }
                catch
                {
                    // Nothing
                }
            }
        }

        public void Dispose()
        {
            // Clean up
            this.FileBlocks.Clear();
            // Clean up our garbage
            GC.Collect();
        }
    }
}