// Copyright (c) Microsoft Corporation
// All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); you may not
// use this file except in compliance with the License.  You may obtain a copy
// of the License at http://www.apache.org/licenses/LICENSE-2.0
// 
// THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY IMPLIED
// WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
// 
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.
namespace Microsoft.Hadoop.Avro.Container
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents a factory for Avro file object containers.
    /// </summary>
    public static class AvroContainer
    {
        /// <summary>
        /// Retrieve Avro Hearder from a stream
        /// </summary>
        /// <param name="avroStream">The stream that contains the avro header</param>
        /// <returns>Avro Header Object</returns>
        public static ObjectContainerHeader GetAvroHeader<TSchema>(Stream avroStream)
        {
            ObjectContainerHeader avroHeader = null;

            avroStream.Seek(0, SeekOrigin.Begin);
            using (var decoder = new BinaryDecoder(avroStream, true))
            {
                avroHeader = ObjectContainerHeader.Read(decoder);
            }
            avroStream.Dispose();
            return avroHeader;
        }


        /// <summary>
        /// Creates Avro Hearder Stream without zero block.
        /// </summary>
        /// <param name="dataList">the list of input data</param>
        /// <param name="schema">The writer schema.</param>
        /// <param name="settings">The serializer settings.</param>
        /// <param name="codec">The codec.</param>
        /// <returns> A writer.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when any argument is null.</exception>
        /// <returns>The first Tuple element is the stream with header, the second tuple element is the avro header</returns>
        public static Tuple<Stream, ObjectContainerHeader> CreateAvroHeaderStream<TSchema>(TSchema t, string schema, AvroSerializerSettings settings, Codec codec, int syncNumberOfObjects)
        {
            ObjectContainerHeader avroHeader = null;
            var stream = new MemoryStream();
            using (var avroWriter = (StreamWriter<TSchema>)AvroContainer.CreateWriter<TSchema>(stream, true, settings, codec, false, null))
            {
                avroWriter.WriteHeader();
                avroHeader = avroWriter.Header;
            }
            stream.Seek(0, SeekOrigin.Begin);
            return new Tuple<Stream, ObjectContainerHeader>(stream, avroHeader);
        }

        /// <summary>
        /// Creates Avro Blocks stream.
        /// </summary>
        /// <param name="dataList">the list of input data</param>
        /// <param name="schema">The writer schema.</param>
        /// <param name="settings">The serializer settings.</param>
        /// <param name="codec">The codec.</param>
        /// <returns> A writer.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when any argument is null.</exception>
        /// <returns>The first Tuple element is the stream with header and blocks, the second tuple element is the avro header</returns>
        public static Tuple<Stream, ObjectContainerHeader> CreateAvroHeaderAndBlockStream<TSchema>(List<TSchema> dataList, string schema, AvroSerializerSettings settings, Codec codec, int syncNumberOfObjects)
        {
            ObjectContainerHeader avroHeader = null;
            var stream = new MemoryStream();
            using (var avroWriter = (StreamWriter<TSchema>)AvroContainer.CreateWriter<TSchema>(stream, true, settings, codec, false, null))
            {
                avroHeader = avroWriter.Header;

                using (var sequentialWriter = new SequentialWriter<TSchema>(avroWriter, syncNumberOfObjects))
                {
                    dataList.ToList().ForEach(sequentialWriter.Write);
                }
            }
            stream.Seek(0, SeekOrigin.Begin);
            return new Tuple<Stream, ObjectContainerHeader>(stream, avroHeader);
        }

        /// <summary>
        /// Creates Avro Blocks and Avro Hearder.
        /// </summary>
        /// <param name="syncMarker">the syncMarker for the existing avro blocks</param>
        /// <param name="dataList">the list of input data</param>
        /// <param name="schema">The writer schema.</param>
        /// <param name="settings">The serializer settings.</param>
        /// <param name="codec">The codec.</param>
        /// <returns> A writer.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when any argument is null.</exception>
        /// <returns>the stream is serialzied bytes of avro blocks</returns>
        public static Stream CreateAvroBlockStream<TSchema>(byte[] syncMarker, List<TSchema> dataList, string schema, AvroSerializerSettings settings, Codec codec, int syncNumberOfObjects)
        {
            ObjectContainerHeader avroHeader = null;
            var stream = new MemoryStream();
            using (var avroWriter = (StreamWriter<TSchema>)AvroContainer.CreateWriter<TSchema>(stream, true, settings, codec, true, syncMarker))
            {
                avroHeader = avroWriter.Header;

                using (var sequentialWriter = new SequentialWriter<TSchema>(avroWriter, syncNumberOfObjects))
                {
                    dataList.ToList().ForEach(sequentialWriter.Write);
                }
            }
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        /// <summary>
        /// Creates a reader of <see cref="Microsoft.Hadoop.Avro.AvroRecord"/> or primitive type.
        /// </summary>
        /// <param name="stream">The stream containing Avro object container.</param>
        /// <returns>A reader.</returns>
        /// <remarks>By default, <paramref name="stream"/> is left open.</remarks>
        public static IAvroReader<object> CreateGenericReader(Stream stream)
        {
            return CreateGenericReader(stream, true);
        }

        /// <summary>
        /// Creates a reader of <see cref="Microsoft.Hadoop.Avro.AvroRecord"/> or primitive type.
        /// </summary>
        /// <param name="stream">The stream containing Avro object container.</param>
        /// <param name="leaveOpen">If set to <c>true</c> leaves the stream open.</param>
        /// <returns> A reader. </returns>
        public static IAvroReader<object> CreateGenericReader(Stream stream, bool leaveOpen)
        {
            return CreateGenericReader(stream, leaveOpen, new CodecFactory());
        }

        /// <summary>
        /// Creates a reader of <see cref="Microsoft.Hadoop.Avro.AvroRecord"/> or primitive type.
        /// </summary>
        /// <param name="stream">The stream containing Avro object container.</param>
        /// <param name="leaveOpen">If set to <c>true</c> leaves the stream open.</param>
        /// <param name="factory">The codec factory.</param>
        /// <returns> A reader. </returns>
        public static IAvroReader<object> CreateGenericReader(Stream stream, bool leaveOpen, CodecFactory factory)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            if (factory == null)
            {
                throw new ArgumentNullException("factory");
            }

            return new StreamReader<object>(null, stream, leaveOpen, factory);
        }

        /// <summary>
        /// Creates a reader of <see cref="Microsoft.Hadoop.Avro.AvroRecord" /> or primitive type.
        /// </summary>
        /// <param name="readerSchema">The reader schema.</param>
        /// <param name="stream">The stream containing Avro object container.</param>
        /// <param name="leaveOpen">If set to <c>true</c> leaves the stream open.</param>
        /// <param name="factory">The codec factory.</param>
        /// <returns> A reader. </returns>
        /// <remarks><paramref name="readerSchema"/> should be specified explicitely when the schema of the consumer/reader is different from the schema of the producer/writer.</remarks>
        public static IAvroReader<object> CreateGenericReader(string readerSchema, Stream stream, bool leaveOpen, CodecFactory factory)
        {
            if (string.IsNullOrEmpty(readerSchema))
            {
                throw new ArgumentNullException("readerSchema");
            }
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            if (factory == null)
            {
                throw new ArgumentNullException("factory");
            }

            return new StreamReader<object>(readerSchema, stream, leaveOpen, factory);
        }

        /// <summary>
        /// Creates a reader for a static C# type.
        /// </summary>
        /// <typeparam name="T">The type of deserialized objects.</typeparam>
        /// <param name="stream">The stream containing Avro object container.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when any argument is null.</exception>
        /// <returns>A reader.</returns>
        /// <remarks>By default, <paramref name="stream"/> is left open and default <see cref="AvroSerializerSettings"/> are used.</remarks>
        public static IAvroReader<T> CreateReader<T>(Stream stream)
        {
            return CreateReader<T>(stream, true);
        }

        /// <summary>
        /// Creates a reader for a static C# type.
        /// </summary>
        /// <typeparam name="T">The type of deserialized objects.</typeparam>
        /// <param name="stream">The stream containing Avro object container.</param>
        /// <param name="leaveOpen">If set to <c>true</c>, the <paramref name="stream"/> is left open.</param>
        /// <returns> A reader. </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when any argument is null.</exception>
        /// <remarks>Default <see cref="AvroSerializerSettings"/> are used.</remarks>
        public static IAvroReader<T> CreateReader<T>(Stream stream, bool leaveOpen)
        {
            return CreateReader<T>(stream, leaveOpen, new AvroSerializerSettings(), new CodecFactory());
        }

        /// <summary>
        /// Creates a reader for a static C# type.
        /// </summary>
        /// <typeparam name="T">The type of deserialized objects.</typeparam>
        /// <param name="stream">The stream containing Avro object container.</param>
        /// <param name="leaveOpen">If set to <c>true</c>, the <paramref name="stream"/> is left open.</param>
        /// <param name="settings">The serializer settings.</param>
        /// <param name="codecFactory">The codec factory.</param>
        /// <returns> A reader. </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when any argument is null.</exception>
        public static IAvroReader<T> CreateReader<T>(Stream stream, bool leaveOpen, AvroSerializerSettings settings, CodecFactory codecFactory)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }
            if (codecFactory == null)
            {
                throw new ArgumentNullException("codecFactory");
            }
            return new StreamReader<T>(stream, leaveOpen, settings, codecFactory);
        }

        /// <summary>
        /// Creates a writer of a static C# type.
        /// </summary>
        /// <typeparam name="T">The type of serialized objects.</typeparam>
        /// <param name="stream">The stream that will contain the resulting Avro object container.</param>
        /// <param name="codec">The codec.</param>
        /// <returns>A writer.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when any argument is null.</exception>
        /// <remarks>By default, the <paramref name="stream"/> is left open and default <see cref="AvroSerializerSettings"/> are used.</remarks>
        public static IAvroWriter<T> CreateWriter<T>(Stream stream, Codec codec)
        {
            return CreateWriter<T>(stream, new AvroSerializerSettings(), codec);
        }

        /// <summary>
        /// Creates a writer of a static C# type.
        /// </summary>
        /// <typeparam name="T">The type of serialized objects.</typeparam>
        /// <param name="stream">The stream that will contain the resulting Avro object container.</param>
        /// <param name="settings">The serializer settings.</param>
        /// <param name="codec">The codec.</param>
        /// <returns> A writer. </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when any argument is null.</exception>
        /// <remarks>By default, the <paramref name="stream"/> is left open.</remarks>
        public static IAvroWriter<T> CreateWriter<T>(Stream stream, AvroSerializerSettings settings, Codec codec)
        {
            return CreateWriter<T>(stream, true, settings, codec);
        }

        /// <summary>
        /// Creates a writer of a static C# type.
        /// </summary>
        /// <typeparam name="T">The type of serialized objects.</typeparam>
        /// <param name="stream">The stream that will contain the resulting Avro object container.</param>
        /// <param name="leaveOpen">If set to <c>true</c> the <paramref name="stream"/> is left open.</param>
        /// <param name="settings">The serializer settings.</param>
        /// <param name="codec">The codec.</param>
        /// <returns> A writer. </returns>
        /// <exception cref="System.ArgumentNullException">Thrown when any argument is null.</exception>
        public static IAvroWriter<T> CreateWriter<T>(Stream stream, bool leaveOpen, AvroSerializerSettings settings, Codec codec, bool isHeaderWritten = false, byte[] syncMarker = null)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            if (settings == null)
            {
                throw new ArgumentNullException("settings");
            }
            if (codec == null)
            {
                throw new ArgumentNullException("codec");
            }
            return new StreamWriter<T>(stream, leaveOpen, AvroSerializer.Create<T>(settings), codec, isHeaderWritten, syncMarker);
        }

        /// <summary>
        /// Creates a writer of <see cref="Microsoft.Hadoop.Avro.AvroRecord"/> or primitive type.
        /// </summary>
        /// <param name="schema">The writer schema.</param>
        /// <param name="stream">The stream that will contain the resulting Avro object container.</param>
        /// <param name="codec">The codec.</param>
        /// <returns> A writer.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when any argument is null.</exception>
        /// <remarks>By default, the <paramref name="stream"/> is left open.</remarks>
        public static IAvroWriter<object> CreateGenericWriter(string schema, Stream stream, Codec codec)
        {
            return CreateGenericWriter(schema, stream, true, codec);
        }

        /// <summary>
        /// Creates a writer of <see cref="Microsoft.Hadoop.Avro.AvroRecord"/> or primitive type.
        /// </summary>
        /// <param name="schema">The writer schema.</param>
        /// <param name="stream">The stream that will contain the resulting Avro object container.</param>
        /// <param name="leaveOpen">If set to <c>true</c> the <paramref name="stream"/> is left open.</param>
        /// <param name="codec">The codec.</param>
        /// <returns> A writer.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when any argument is null.</exception>
        public static IAvroWriter<object> CreateGenericWriter(string schema, Stream stream, bool leaveOpen, Codec codec)
        {
            if (string.IsNullOrEmpty(schema))
            {
                throw new ArgumentNullException("schema");
            }
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            if (codec == null)
            {
                throw new ArgumentNullException("codec");
            }
            return new StreamWriter<object>(stream, leaveOpen, AvroSerializer.CreateGeneric(schema), codec);
        }
    }
}