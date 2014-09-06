using System.Collections.Generic;

namespace Lucene.Net.Index
{
    /*
     * Licensed to the Apache Software Foundation (ASF) under one or more
     * contributor license agreements.  See the NOTICE file distributed with
     * this work for additional information regarding copyright ownership.
     * The ASF licenses this file to You under the Apache License, Version 2.0
     * (the "License"); you may not use this file except in compliance with
     * the License.  You may obtain a copy of the License at
     *
     *     http://www.apache.org/licenses/LICENSE-2.0
     *
     * Unless required by applicable law or agreed to in writing, software
     * distributed under the License is distributed on an "AS IS" BASIS,
     * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
     * See the License for the specific language governing permissions and
     * limitations under the License.
     */

    /// <summary>
    /// this is a DocFieldConsumer that inverts each field,
    ///  separately, from a Document, and accepts a
    ///  InvertedTermsConsumer to process those terms.
    /// </summary>
    public sealed class DocInverter : DocFieldConsumer
    {
        internal readonly InvertedDocConsumer Consumer;
        internal readonly InvertedDocEndConsumer EndConsumer;

        internal readonly DocumentsWriterPerThread.DocState DocState;

        public DocInverter(DocumentsWriterPerThread.DocState docState, InvertedDocConsumer consumer, InvertedDocEndConsumer endConsumer)
        {
            this.DocState = docState;
            this.Consumer = consumer;
            this.EndConsumer = endConsumer;
        }

        public override void Flush(IDictionary<string, DocFieldConsumerPerField> fieldsToFlush, SegmentWriteState state)
        {
            IDictionary<string, InvertedDocConsumerPerField> childFieldsToFlush = new Dictionary<string, InvertedDocConsumerPerField>();
            IDictionary<string, InvertedDocEndConsumerPerField> endChildFieldsToFlush = new Dictionary<string, InvertedDocEndConsumerPerField>();

            foreach (KeyValuePair<string, DocFieldConsumerPerField> fieldToFlush in fieldsToFlush)
            {
                DocInverterPerField perField = (DocInverterPerField)fieldToFlush.Value;
                childFieldsToFlush[fieldToFlush.Key] = perField.Consumer;
                endChildFieldsToFlush[fieldToFlush.Key] = perField.EndConsumer;
            }

            Consumer.Flush(childFieldsToFlush, state);
            EndConsumer.Flush(endChildFieldsToFlush, state);
        }

        public override void StartDocument()
        {
            Consumer.StartDocument();
            EndConsumer.StartDocument();
        }

        public override void FinishDocument()
        {
            // TODO: allow endConsumer.finishDocument to also return
            // a DocWriter
            EndConsumer.FinishDocument();
            Consumer.FinishDocument();
        }

        public override void Abort()
        {
            try
            {
                Consumer.Abort();
            }
            finally
            {
                EndConsumer.Abort();
            }
        }

        public override DocFieldConsumerPerField AddField(FieldInfo fi)
        {
            return new DocInverterPerField(this, fi);
        }
    }
}