using System;
using Lucene.Net.Support;
using Lucene.Net.Support.Compatibility;

namespace Lucene.Net.Util
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


	using Document = Lucene.Net.Document.Document;
	using Field = Lucene.Net.Document.Field;
	using FieldType = Lucene.Net.Document.FieldType;
	using SortedDocValuesField = Lucene.Net.Document.SortedDocValuesField;
	using StringField = Lucene.Net.Document.StringField;
	using TextField = Lucene.Net.Document.TextField;
    using Lucene.Net.Randomized.Generators;
    using System.Threading;

	/// <summary>
	/// Minimal port of benchmark's LneDocSource +
	/// DocMaker, so tests can enum docs from a line file created
	/// by benchmark's WriteLineDoc task 
	/// </summary>
	public class LineFileDocs : IDisposable
	{

	  private BufferedReader Reader;
	  private static readonly int BUFFER_SIZE = 1 << 16; // 64K
	  private readonly AtomicInteger Id = new AtomicInteger();
	  private readonly string Path;
	  private readonly bool UseDocValues;

	  /// <summary>
	  /// If forever is true, we rewind the file at EOF (repeat
	  /// the docs over and over) 
	  /// </summary>
	  public LineFileDocs(Random random, string path, bool useDocValues)
	  {
		this.Path = path;
		this.UseDocValues = useDocValues;
		Open(random);
	  }

	  public LineFileDocs(Random random) : this(random, LuceneTestCase.TEST_LINE_DOCS_FILE, true)
	  {
	  }

	  public LineFileDocs(Random random, bool useDocValues) : this(random, LuceneTestCase.TEST_LINE_DOCS_FILE, useDocValues)
	  {
	  }

	  public void Dispose()
	  {
		  lock (this)
		  {
			if (Reader != null)
			{
			  Reader.Close();
			  Reader = null;
			}
		  }
	  }

	  private long RandomSeekPos(Random random, long size)
	  {
		if (random == null || size <= 3L)
		{
		  return 0L;
		}
		return (random.NextLong() & long.MaxValue) % (size / 3);
	  }

	  private void Open(Random random)
	  {
		  lock (this)
		  {
			InputStream @is = this.GetType().getResourceAsStream(Path);
			bool needSkip = true;
			long size = 0L, seekTo = 0L;
			if (@is == null)
			{
			  // if its not in classpath, we load it as absolute filesystem path (e.g. Hudson's home dir)
			  File file = new File(Path);
			  size = file.length();
			  if (Path.EndsWith(".gz"))
			  {
				// if it is a gzip file, we need to use InputStream and slowly skipTo:
				@is = new FileInputStream(file);
			  }
			  else
			  {
				// optimized seek using RandomAccessFile:
				seekTo = RandomSeekPos(random, size);
				FileChannel channel = (new RandomAccessFile(Path, "r")).Channel;
				if (LuceneTestCase.VERBOSE)
				{
				  Console.WriteLine("TEST: LineFileDocs: file seek to fp=" + seekTo + " on open");
				}
				channel.position(seekTo);
				@is = Channels.newInputStream(channel);
				needSkip = false;
			  }
			}
			else
			{
			  // if the file comes from Classpath:
			  size = @is.available();
			}
        
			if (Path.EndsWith(".gz"))
			{
			  @is = new GZIPInputStream(@is);
			  // guestimate:
			  size *= 2.8;
			}
        
			// If we only have an InputStream, we need to seek now,
			// but this seek is a scan, so very inefficient!!!
			if (needSkip)
			{
			  seekTo = RandomSeekPos(random, size);
			  if (LuceneTestCase.VERBOSE)
			  {
				Console.WriteLine("TEST: LineFileDocs: stream skip to fp=" + seekTo + " on open");
			  }
			  @is.Skip(seekTo);
			}
        
			// if we seeked somewhere, read until newline char
			if (seekTo > 0L)
			{
			  int b;
			  do
			  {
				b = @is.read();
			  } while (b >= 0 && b != 13 && b != 10);
			}
        
			CharsetDecoder decoder = StandardCharsets.UTF_8.newDecoder().onMalformedInput(CodingErrorAction.REPORT).onUnmappableCharacter(CodingErrorAction.REPORT);
			Reader = new BufferedReader(new InputStreamReader(@is, decoder), BUFFER_SIZE);
        
			if (seekTo > 0L)
			{
			  // read one more line, to make sure we are not inside a Windows linebreak (\r\n):
			  Reader.readLine();
			}
		  }
	  }

	  public virtual void Reset(Random random)
	  {
		  lock (this)
		  {
			Dispose();
			Open(random);
			Id.Set(0);
		  }
	  }

	  private const char SEP = '\t';

	  private sealed class DocState
	  {
		internal readonly Document Doc;
		internal readonly Field TitleTokenized;
		internal readonly Field Title;
		internal readonly Field TitleDV;
		internal readonly Field Body;
		internal readonly Field Id;
		internal readonly Field Date;

		public DocState(bool useDocValues)
		{
		  Doc = new Document();

		  Title = new StringField("title", "", Field.Store.NO);
		  Doc.Add(Title);

		  FieldType ft = new FieldType(TextField.TYPE_STORED);
		  ft.StoreTermVectors = true;
		  ft.StoreTermVectorOffsets = true;
		  ft.StoreTermVectorPositions = true;

		  TitleTokenized = new Field("titleTokenized", "", ft);
          Doc.Add(TitleTokenized);

		  Body = new Field("body", "", ft);
          Doc.Add(Body);

		  Id = new StringField("docid", "", Field.Store.YES);
          Doc.Add(Id);

		  Date = new StringField("date", "", Field.Store.YES);
          Doc.Add(Date);

		  if (useDocValues)
		  {
			TitleDV = new SortedDocValuesField("titleDV", new BytesRef());
            Doc.Add(TitleDV);
		  }
		  else
		  {
			TitleDV = null;
		  }
		}
	  }

	  private readonly ThreadLocal<DocState> ThreadDocs = new ThreadLocal<DocState>();

	  /// <summary>
	  /// Note: Document instance is re-used per-thread </summary>
	  public virtual Document NextDoc()
	  {
		string line;
		lock (this)
		{
		  line = Reader.readLine();
		  if (line == null)
		  {
			// Always rewind at end:
			if (LuceneTestCase.VERBOSE)
			{
			  Console.WriteLine("TEST: LineFileDocs: now rewind file...");
			}
			Dispose();
			Open(null);
			line = Reader.readLine();
		  }
		}

		DocState docState = ThreadDocs.Get();
		if (docState == null)
		{
		  docState = new DocState(UseDocValues);
		  ThreadDocs.set(docState);
		}

		int spot = line.IndexOf(SEP);
		if (spot == -1)
		{
		  throw new Exception("line: [" + line + "] is in an invalid format !");
		}
		int spot2 = line.IndexOf(SEP, 1 + spot);
		if (spot2 == -1)
		{
		  throw new Exception("line: [" + line + "] is in an invalid format !");
		}

		docState.Body.StringValue = line.Substring(1 + spot2, line.Length - (1 + spot2));
		string title = line.Substring(0, spot);
		docState.Title.StringValue = title;
		if (docState.TitleDV != null)
		{
		  docState.TitleDV.BytesValue = new BytesRef(title);
		}
		docState.TitleTokenized.StringValue = title;
		docState.Date.StringValue = line.Substring(1 + spot, spot2 - (1 + spot));
		docState.Id.StringValue = Convert.ToString(Id.IncrementAndGet());
		return docState.Doc;
	  }
	}

}