namespace AvroTests
{
    using System.Runtime.Serialization;

    [DataContract]
    public class MyDataType
    {
        public MyDataType()
        {
        }

        public MyDataType(int i)
        {
            SomeNumber = i;
            var c = (char)((i % 26) + 65);
            SomeString = c.ToString();
        }

        [DataMember]
        public int SomeNumber { get; set; }

        [DataMember]
        public string SomeString { get; set; }
    }
}