using System.IO;
using Systems.Inventory;

public static class BinaryReaderExtensions 
{
    public static SerializableGuid ReadSerializableGuid(this BinaryReader reader) 
    {
        return new SerializableGuid(
            reader.ReadUInt32(), 
            reader.ReadUInt32(), 
            reader.ReadUInt32(), 
            reader.ReadUInt32()
        );
    }
}