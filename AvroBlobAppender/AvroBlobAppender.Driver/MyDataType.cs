using Microsoft.Hadoop.Avro;
using Microsoft.Hadoop.Avro.Container;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AvroBlobAppender.Driver
{
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


        public static List<MyDataType> GenerateData(int start, int count)
        {
            List<MyDataType> myDatas = new List<MyDataType>();

            return Enumerable.Range(start, count).Select(x => new MyDataType(x)).ToList();
        }

        public static bool Equal(MyDataType left, MyDataType right)
        {
            return left.SomeNumber.Equals(right.SomeNumber) && left.SomeString.SequenceEqual(right.SomeString);
        }
    }
}
