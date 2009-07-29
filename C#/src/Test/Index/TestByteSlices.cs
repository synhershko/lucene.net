/**
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using NUnit.Framework;

using LuceneTestCase = Lucene.Net.Util.LuceneTestCase;

namespace Lucene.Net.Index
{
    public class TestByteSlices : LuceneTestCase
    {

        private class ByteBlockAllocator : ByteBlockPool.Allocator
        {
            System.Collections.Generic.List<byte[]> freeByteBlocks = new System.Collections.Generic.List<byte[]>();

            /* Allocate another byte[] from the shared pool */
            override public byte[] GetByteBlock(bool trackAllocations)
            {
                lock (this)
                {
                    int size = freeByteBlocks.Count;
                    byte[] b;
                    if (0 == size)
                        b = new byte[DocumentsWriter.BYTE_BLOCK_SIZE_For_NUnit_Test];
                    else
                    {
                        b = freeByteBlocks[size - 1];
                        freeByteBlocks.RemoveAt(size - 1);
                    }
                    return b;
                }
            }

            /* Return a byte[] to the pool */
            override public void RecycleByteBlocks(byte[][] blocks, int start, int end)
            {
                lock (this)
                {
                    for (int i = start; i < end; i++)
                        freeByteBlocks.Add(blocks[i]);
                }
            }
        }

        [Test]
        public void TestBasic()
        {
            ByteBlockPool pool = new ByteBlockPool(new ByteBlockAllocator(), false);

            int NUM_STREAM = 25;

            ByteSliceWriter writer = new ByteSliceWriter(pool);

            int[] starts = new int[NUM_STREAM];
            int[] uptos = new int[NUM_STREAM];
            int[] counters = new int[NUM_STREAM];

            System.Random r = new System.Random(1);

            ByteSliceReader reader = new ByteSliceReader();

            for (int ti = 0; ti < 100; ti++)
            {

                for (int stream = 0; stream < NUM_STREAM; stream++)
                {
                    starts[stream] = -1;
                    counters[stream] = 0;
                }

                bool debug = false;

                for (int iter = 0; iter < 10000; iter++)
                {
                    int stream = r.Next(NUM_STREAM);
                    if (debug)
                        System.Console.WriteLine("write stream=" + stream);

                    if (starts[stream] == -1)
                    {
                        int spot = pool.NewSlice(ByteBlockPool.FIRST_LEVEL_SIZE_For_NUnit_Test);
                        starts[stream] = uptos[stream] = spot + pool.byteOffset;
                        if (debug)
                            System.Console.WriteLine("  init to " + starts[stream]);
                    }

                    writer.Init(uptos[stream]);
                    int numValue = r.Next(20);
                    for (int j = 0; j < numValue; j++)
                    {
                        if (debug)
                            System.Console.WriteLine("    write " + (counters[stream] + j));
                        writer.WriteVInt(counters[stream] + j);
                        //writer.writeVInt(ti);
                    }
                    counters[stream] += numValue;
                    uptos[stream] = writer.GetAddress();
                    if (debug)
                        System.Console.WriteLine("    addr now " + uptos[stream]);
                }

                for (int stream = 0; stream < NUM_STREAM; stream++)
                {
                    if (debug)
                        System.Console.WriteLine("  stream=" + stream + " count=" + counters[stream]);

                    if (starts[stream] != uptos[stream])
                    {
                        reader.Init(pool, starts[stream], uptos[stream]);
                        for (int j = 0; j < counters[stream]; j++)
                            Assert.AreEqual(j, reader.ReadVInt());
                        //assertEquals(ti, reader.readVInt());
                    }
                }

                pool.Reset();
            }
        }
    }
}