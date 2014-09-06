using Lucene.Net.Support;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Lucene.Net.Util.Fst
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
    /*
	using Lucene.Net.Util.Fst.FST;
	using BytesReader = Lucene.Net.Util.Fst.FST.BytesReader;
    */

    /// <summary>
    /// Static helper methods.
    ///
    /// @lucene.experimental
    /// </summary>
    public sealed class Util
    {
        private Util()
        {
        }

        /// <summary>
        /// Looks up the output for this input, or null if the
        ///  input is not accepted.
        /// </summary>
        public static T Get<T>(FST<T> fst, IntsRef input)
        {
            // TODO: would be nice not to alloc this on every lookup
            var arc = fst.GetFirstArc(new FST<T>.Arc<T>());

            var fstReader = fst.GetBytesReader;

            // Accumulate output as we go
            T output = fst.Outputs.NoOutput;
            for (int i = 0; i < input.Length; i++)
            {
                if (fst.FindTargetArc(input.Ints[input.Offset + i], arc, arc, fstReader) == null)
                {
                    return default(T);
                }
                output = fst.Outputs.Add(output, arc.Output);
            }

            if (arc.Final)
            {
                return fst.Outputs.Add(output, arc.NextFinalOutput);
            }
            else
            {
                return default(T);
            }
        }

        // TODO: maybe a CharsRef version for BYTE2

        /// <summary>
        /// Looks up the output for this input, or null if the
        ///  input is not accepted
        /// </summary>
        public static T Get<T>(FST<T> fst, BytesRef input)
        {
            Debug.Assert(fst.inputType == FST<long>.INPUT_TYPE.BYTE1);

            var fstReader = fst.GetBytesReader;

            // TODO: would be nice not to alloc this on every lookup
            var arc = fst.GetFirstArc(new FST<T>.Arc<T>());

            // Accumulate output as we go
            T output = fst.Outputs.NoOutput;
            for (int i = 0; i < input.Length; i++)
            {
                if (fst.FindTargetArc(input.Bytes[i + input.Offset] & 0xFF, arc, arc, fstReader) == null)
                {
                    return default(T);
                }
                output = fst.Outputs.Add(output, arc.Output);
            }

            if (arc.Final)
            {
                return fst.Outputs.Add(output, arc.NextFinalOutput);
            }
            else
            {
                return default(T);
            }
        }

        /// <summary>
        /// Reverse lookup (lookup by output instead of by input),
        ///  in the special case when your FSTs outputs are
        ///  strictly ascending.  this locates the input/output
        ///  pair where the output is equal to the target, and will
        ///  return null if that output does not exist.
        ///
        ///  <p>NOTE: this only works with {@code FST<Long>}, only
        ///  works when the outputs are ascending in order with
        ///  the inputs.
        ///  For example, simple ordinals (0, 1,
        ///  2, ...), or file offets (when appending to a file)
        ///  fit this.
        /// </summary>
        public static IntsRef GetByOutput(FST<long> fst, long targetOutput)
        {
            var @in = fst.GetBytesReader;

            // TODO: would be nice not to alloc this on every lookup
            FST<long>.Arc<long> arc = fst.GetFirstArc(new FST<long>.Arc<long>());

            FST<long>.Arc<long> scratchArc = new FST<long>.Arc<long>();

            IntsRef result = new IntsRef();

            return GetByOutput(fst, targetOutput, @in, arc, scratchArc, result);
        }

        /// <summary>
        /// Expert: like <seealso cref="Util#getByOutput(FST, long)"/> except reusing
        /// BytesReader, initial and scratch Arc, and result.
        /// </summary>
        public static IntsRef GetByOutput(FST<long> fst, long targetOutput, FST<long>.BytesReader @in, FST<long>.Arc<long> arc, FST<long>.Arc<long> scratchArc, IntsRef result)
        {
            long output = arc.Output;
            int upto = 0;

            //System.out.println("reverseLookup output=" + targetOutput);

            while (true)
            {
                //System.out.println("loop: output=" + output + " upto=" + upto + " arc=" + arc);
                if (arc.Final)
                {
                    long finalOutput = output + arc.NextFinalOutput;
                    //System.out.println("  isFinal finalOutput=" + finalOutput);
                    if (finalOutput == targetOutput)
                    {
                        result.Length = upto;
                        //System.out.println("    found!");
                        return result;
                    }
                    else if (finalOutput > targetOutput)
                    {
                        //System.out.println("    not found!");
                        return null;
                    }
                }

                if (FST<long>.TargetHasArcs(arc))
                {
                    //System.out.println("  targetHasArcs");
                    if (result.Ints.Length == upto)
                    {
                        result.Grow(1 + upto);
                    }

                    fst.ReadFirstRealTargetArc(arc.Target, arc, @in);

                    if (arc.BytesPerArc != 0)
                    {
                        int low = 0;
                        int high = arc.NumArcs - 1;
                        int mid = 0;
                        //System.out.println("bsearch: numArcs=" + arc.numArcs + " target=" + targetOutput + " output=" + output);
                        bool exact = false;
                        while (low <= high)
                        {
                            mid = (int)((uint)(low + high) >> 1);
                            @in.Position = arc.PosArcsStart;
                            @in.SkipBytes(arc.BytesPerArc * mid);
                            sbyte flags = @in.ReadSByte();
                            fst.ReadLabel(@in);
                            long minArcOutput;
                            if ((flags & FST<long>.BIT_ARC_HAS_OUTPUT) != 0)
                            {
                                long arcOutput = fst.Outputs.Read(@in);
                                minArcOutput = output + arcOutput;
                            }
                            else
                            {
                                minArcOutput = output;
                            }
                            if (minArcOutput == targetOutput)
                            {
                                exact = true;
                                break;
                            }
                            else if (minArcOutput < targetOutput)
                            {
                                low = mid + 1;
                            }
                            else
                            {
                                high = mid - 1;
                            }
                        }

                        if (high == -1)
                        {
                            return null;
                        }
                        else if (exact)
                        {
                            arc.ArcIdx = mid - 1;
                        }
                        else
                        {
                            arc.ArcIdx = low - 2;
                        }

                        fst.ReadNextRealArc(arc, @in);
                        result.Ints[upto++] = arc.Label;
                        output += arc.Output;
                    }
                    else
                    {
                        FST<long>.Arc<long> prevArc = null;

                        while (true)
                        {
                            //System.out.println("    cycle label=" + arc.label + " output=" + arc.output);

                            // this is the min output we'd hit if we follow
                            // this arc:
                            long minArcOutput = output + arc.Output;

                            if (minArcOutput == targetOutput)
                            {
                                // Recurse on this arc:
                                //System.out.println("  match!  break");
                                output = minArcOutput;
                                result.Ints[upto++] = arc.Label;
                                break;
                            }
                            else if (minArcOutput > targetOutput)
                            {
                                if (prevArc == null)
                                {
                                    // Output doesn't exist
                                    return null;
                                }
                                else
                                {
                                    // Recurse on previous arc:
                                    arc.CopyFrom(prevArc);
                                    result.Ints[upto++] = arc.Label;
                                    output += arc.Output;
                                    //System.out.println("    recurse prev label=" + (char) arc.label + " output=" + output);
                                    break;
                                }
                            }
                            else if (arc.Last)
                            {
                                // Recurse on this arc:
                                output = minArcOutput;
                                //System.out.println("    recurse last label=" + (char) arc.label + " output=" + output);
                                result.Ints[upto++] = arc.Label;
                                break;
                            }
                            else
                            {
                                // Read next arc in this node:
                                prevArc = scratchArc;
                                prevArc.CopyFrom(arc);
                                //System.out.println("      after copy label=" + (char) prevArc.label + " vs " + (char) arc.label);
                                fst.ReadNextRealArc(arc, @in);
                            }
                        }
                    }
                }
                else
                {
                    //System.out.println("  no target arcs; not found!");
                    return null;
                }
            }
        }

        /// <summary>
        /// Represents a path in TopNSearcher.
        ///
        ///  @lucene.experimental
        /// </summary>
        public class FSTPath<T>
        {
            public FST<T>.Arc<T> Arc;
            public T Cost;
            public readonly IntsRef Input;

            /// <summary>
            /// Sole constructor </summary>
            public FSTPath(T cost, FST<T>.Arc<T> arc, IntsRef input)
            {
                this.Arc = (new FST<T>.Arc<T>()).CopyFrom(arc);
                this.Cost = cost;
                this.Input = input;
            }

            public override string ToString()
            {
                return "input=" + Input + " cost=" + Cost;
            }
        }

        /// <summary>
        /// Compares first by the provided comparator, and then
        ///  tie breaks by path.input.
        /// </summary>
        private class TieBreakByInputComparator<T> : IComparer<FSTPath<T>>
        {
            internal readonly IComparer<T> Comparator;

            public TieBreakByInputComparator(IComparer<T> comparator)
            {
                this.Comparator = comparator;
            }

            public virtual int Compare(FSTPath<T> a, FSTPath<T> b)
            {
                int cmp = Comparator.Compare(a.Cost, b.Cost);
                if (cmp == 0)
                {
                    return a.Input.CompareTo(b.Input);
                }
                else
                {
                    return cmp;
                }
            }
        }

        /// <summary>
        /// Utility class to find top N shortest paths from start
        ///  point(s).
        /// </summary>
        public class TopNSearcher<T>
        {
            internal readonly FST<T> Fst;
            internal readonly FST<T>.BytesReader BytesReader;
            internal readonly int TopN;
            internal readonly int MaxQueueDepth;

            internal readonly FST<T>.Arc<T> ScratchArc = new FST<T>.Arc<T>();

            internal readonly IComparer<T> Comparator;

            internal SortedSet<FSTPath<T>> Queue = null;

            /// <summary>
            /// Creates an unbounded TopNSearcher </summary>
            /// <param name="fst"> the <seealso cref="Lucene.Net.Util.Fst.FST"/> to search on </param>
            /// <param name="topN"> the number of top scoring entries to retrieve </param>
            /// <param name="maxQueueDepth"> the maximum size of the queue of possible top entries </param>
            /// <param name="comparator"> the comparator to select the top N </param>
            public TopNSearcher(FST<T> fst, int topN, int maxQueueDepth, IComparer<T> comparator)
            {
                this.Fst = fst;
                this.BytesReader = fst.GetBytesReader;
                this.TopN = topN;
                this.MaxQueueDepth = maxQueueDepth;
                this.Comparator = comparator;

                Queue = new SortedSet<FSTPath<T>>(new TieBreakByInputComparator<T>(comparator));
            }

            // If back plus this arc is competitive then add to queue:
            protected internal virtual void AddIfCompetitive(FSTPath<T> path)
            {
                Debug.Assert(Queue != null);

                T cost = Fst.Outputs.Add(path.Cost, path.Arc.Output);
                //System.out.println("  addIfCompetitive queue.size()=" + queue.size() + " path=" + path + " + label=" + path.arc.label);

                if (Queue.Count == MaxQueueDepth)
                {
                    FSTPath<T> bottom = Queue.Max;
                    int comp = Comparator.Compare(cost, bottom.Cost);
                    if (comp > 0)
                    {
                        // Doesn't compete
                        return;
                    }
                    else if (comp == 0)
                    {
                        // Tie break by alpha sort on the input:
                        path.Input.Grow(path.Input.Length + 1);
                        path.Input.Ints[path.Input.Length++] = path.Arc.Label;
                        int cmp = bottom.Input.CompareTo(path.Input);
                        path.Input.Length--;

                        // We should never see dups:
                        Debug.Assert(cmp != 0);

                        if (cmp < 0)
                        {
                            // Doesn't compete
                            return;
                        }
                    }
                    // Competes
                }
                else
                {
                    // Queue isn't full yet, so any path we hit competes:
                }

                // copy over the current input to the new input
                // and add the arc.label to the end
                IntsRef newInput = new IntsRef(path.Input.Length + 1);
                Array.Copy(path.Input.Ints, 0, newInput.Ints, 0, path.Input.Length);
                newInput.Ints[path.Input.Length] = path.Arc.Label;
                newInput.Length = path.Input.Length + 1;
                FSTPath<T> newPath = new FSTPath<T>(cost, path.Arc, newInput);

                Queue.Add(newPath);

                if (Queue.Count == MaxQueueDepth + 1)
                {
                    Queue.Last();
                }
            }

            /// <summary>
            /// Adds all leaving arcs, including 'finished' arc, if
            ///  the node is final, from this node into the queue.
            /// </summary>
            public virtual void AddStartPaths(FST<T>.Arc<T> node, T startOutput, bool allowEmptyString, IntsRef input)
            {
                // De-dup NO_OUTPUT since it must be a singleton:
                if (startOutput.Equals(Fst.Outputs.NoOutput))
                {
                    startOutput = Fst.Outputs.NoOutput;
                }

                FSTPath<T> path = new FSTPath<T>(startOutput, node, input);
                Fst.ReadFirstTargetArc(node, path.Arc, BytesReader);

                //System.out.println("add start paths");

                // Bootstrap: find the min starting arc
                while (true)
                {
                    if (allowEmptyString || path.Arc.Label != FST<T>.END_LABEL)
                    {
                        AddIfCompetitive(path);
                    }
                    if (path.Arc.Last)
                    {
                        break;
                    }
                    Fst.ReadNextArc(path.Arc, BytesReader);
                }
            }

            public virtual TopResults<T> Search()
            {
                IList<Result<T>> results = new List<Result<T>>();

                //System.out.println("search topN=" + topN);

                var fstReader = Fst.GetBytesReader;
                T NO_OUTPUT = Fst.Outputs.NoOutput;

                // TODO: we could enable FST to sorting arcs by weight
                // as it freezes... can easily do this on first pass
                // (w/o requiring rewrite)

                // TODO: maybe we should make an FST.INPUT_TYPE.BYTE0.5!?
                // (nibbles)
                int rejectCount = 0;

                // For each top N path:
                while (results.Count < TopN)
                {
                    //System.out.println("\nfind next path: queue.size=" + queue.size());

                    FSTPath<T> path;

                    if (Queue == null)
                    {
                        // Ran out of paths
                        //System.out.println("  break queue=null");
                        break;
                    }

                    // Remove top path since we are now going to
                    // pursue it:
                    path = Queue.First();

                    if (path == null)
                    {
                        // There were less than topN paths available:
                        //System.out.println("  break no more paths");
                        break;
                    }

                    if (path.Arc.Label == FST<T>.END_LABEL)
                    {
                        //System.out.println("    empty string!  cost=" + path.cost);
                        // Empty string!
                        path.Input.Length--;
                        results.Add(new Result<T>(path.Input, path.Cost));
                        continue;
                    }

                    if (results.Count == TopN - 1 && MaxQueueDepth == TopN)
                    {
                        // Last path -- don't bother w/ queue anymore:
                        Queue = null;
                    }

                    //System.out.println("  path: " + path);

                    // We take path and find its "0 output completion",
                    // ie, just keep traversing the first arc with
                    // NO_OUTPUT that we can find, since this must lead
                    // to the minimum path that completes from
                    // path.arc.

                    // For each input letter:
                    while (true)
                    {
                        //System.out.println("\n    cycle path: " + path);
                        Fst.ReadFirstTargetArc(path.Arc, path.Arc, fstReader);

                        // For each arc leaving this node:
                        bool foundZero = false;
                        while (true)
                        {
                            //System.out.println("      arc=" + (char) path.arc.label + " cost=" + path.arc.output);
                            // tricky: instead of comparing output == 0, we must
                            // express it via the comparator compare(output, 0) == 0
                            if (Comparator.Compare(NO_OUTPUT, path.Arc.Output) == 0)
                            {
                                if (Queue == null)
                                {
                                    foundZero = true;
                                    break;
                                }
                                else if (!foundZero)
                                {
                                    ScratchArc.CopyFrom(path.Arc);
                                    foundZero = true;
                                }
                                else
                                {
                                    AddIfCompetitive(path);
                                }
                            }
                            else if (Queue != null)
                            {
                                AddIfCompetitive(path);
                            }
                            if (path.Arc.Last)
                            {
                                break;
                            }
                            Fst.ReadNextArc(path.Arc, fstReader);
                        }

                        Debug.Assert(foundZero);

                        if (Queue != null)
                        {
                            // TODO: maybe we can save this copyFrom if we
                            // are more clever above... eg on finding the
                            // first NO_OUTPUT arc we'd switch to using
                            // scratchArc
                            path.Arc.CopyFrom(ScratchArc);
                        }

                        if (path.Arc.Label == FST<T>.END_LABEL)
                        {
                            // Add final output:
                            //System.out.println("    done!: " + path);
                            T finalOutput = Fst.Outputs.Add(path.Cost, path.Arc.Output);
                            if (AcceptResult(path.Input, finalOutput))
                            {
                                //System.out.println("    add result: " + path);
                                results.Add(new Result<T>(path.Input, finalOutput));
                            }
                            else
                            {
                                rejectCount++;
                            }
                            break;
                        }
                        else
                        {
                            path.Input.Grow(1 + path.Input.Length);
                            path.Input.Ints[path.Input.Length] = path.Arc.Label;
                            path.Input.Length++;
                            path.Cost = Fst.Outputs.Add(path.Cost, path.Arc.Output);
                        }
                    }
                }
                return new TopResults<T>(rejectCount + TopN <= MaxQueueDepth, results);
            }

            protected internal virtual bool AcceptResult(IntsRef input, T output)
            {
                return true;
            }
        }

        /// <summary>
        /// Holds a single input (IntsRef) + output, returned by
        ///  <seealso cref="#shortestPaths shortestPaths()"/>.
        /// </summary>
        public sealed class Result<T>
        {
            public readonly IntsRef Input;
            public readonly T Output;

            public Result(IntsRef input, T output)
            {
                this.Input = input;
                this.Output = output;
            }
        }

        /// <summary>
        /// Holds the results for a top N search using <seealso cref="TopNSearcher"/>
        /// </summary>
        public sealed class TopResults<T> : IEnumerable<Result<T>>
        {
            /// <summary>
            /// <code>true</code> iff this is a complete result ie. if
            /// the specified queue size was large enough to find the complete list of results. this might
            /// be <code>false</code> if the <seealso cref="TopNSearcher"/> rejected too many results.
            /// </summary>
            public readonly bool IsComplete;

            /// <summary>
            /// The top results
            /// </summary>
            public readonly IList<Result<T>> TopN;

            internal TopResults(bool isComplete, IList<Result<T>> topN)
            {
                this.TopN = topN;
                this.IsComplete = isComplete;
            }

            public IEnumerator<Result<T>> GetEnumerator()
            {
                return TopN.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        /// <summary>
        /// Starting from node, find the top N min cost
        ///  completions to a final node.
        /// </summary>
        public static TopResults<T> shortestPaths<T>(FST<T> fst, FST<T>.Arc<T> fromNode, T startOutput, IComparer<T> comparator, int topN, bool allowEmptyString)
        {
            // All paths are kept, so we can pass topN for
            // maxQueueDepth and the pruning is admissible:
            TopNSearcher<T> searcher = new TopNSearcher<T>(fst, topN, topN, comparator);

            // since this search is initialized with a single start node
            // it is okay to start with an empty input path here
            searcher.AddStartPaths(fromNode, startOutput, allowEmptyString, new IntsRef());
            return searcher.Search();
        }

        /// <summary>
        /// Dumps an <seealso cref="FST"/> to a GraphViz's <code>dot</code> language description
        /// for visualization. Example of use:
        ///
        /// <pre class="prettyprint">
        /// PrintWriter pw = new PrintWriter(&quot;out.dot&quot;);
        /// Util.toDot(fst, pw, true, true);
        /// pw.close();
        /// </pre>
        ///
        /// and then, from command line:
        ///
        /// <pre>
        /// dot -Tpng -o out.png out.dot
        /// </pre>
        ///
        /// <p>
        /// Note: larger FSTs (a few thousand nodes) won't even
        /// render, don't bother.  If the FST is > 2.1 GB in size
        /// then this method will throw strange exceptions.
        /// </summary>
        /// <param name="sameRank">
        ///          If <code>true</code>, the resulting <code>dot</code> file will try
        ///          to order states in layers of breadth-first traversal. this may
        ///          mess up arcs, but makes the output FST's structure a bit clearer.
        /// </param>
        /// <param name="labelStates">
        ///          If <code>true</code> states will have labels equal to their offsets in their
        ///          binary format. Expands the graph considerably.
        /// </param>
        /// <seealso cref= "http://www.graphviz.org/" </seealso>
        public static void toDot<T>(FST<T> fst, TextWriter @out, bool sameRank, bool labelStates)
        {
            const string expandedNodeColor = "blue";

            // this is the start arc in the automaton (from the epsilon state to the first state
            // with outgoing transitions.
            FST<T>.Arc<T> startArc = fst.GetFirstArc(new FST<T>.Arc<T>());

            // A queue of transitions to consider for the next level.
            IList<FST<T>.Arc<T>> thisLevelQueue = new List<FST<T>.Arc<T>>();

            // A queue of transitions to consider when processing the next level.
            IList<FST<T>.Arc<T>> nextLevelQueue = new List<FST<T>.Arc<T>>();
            nextLevelQueue.Add(startArc);
            //System.out.println("toDot: startArc: " + startArc);

            // A list of states on the same level (for ranking).
            IList<int?> sameLevelStates = new List<int?>();

            // A bitset of already seen states (target offset).
            BitArray seen = new BitArray(32);
            seen.Set((int)startArc.Target, true);

            // Shape for states.
            const string stateShape = "circle";
            const string finalStateShape = "doublecircle";

            // Emit DOT prologue.
            @out.Write("digraph FST {\n");
            @out.Write("  rankdir = LR; splines=true; concentrate=true; ordering=out; ranksep=2.5; \n");

            if (!labelStates)
            {
                @out.Write("  node [shape=circle, width=.2, height=.2, style=filled]\n");
            }

            EmitDotState(@out, "initial", "point", "white", "");

            T NO_OUTPUT = fst.Outputs.NoOutput;
            var r = fst.GetBytesReader;

            // final FST.Arc<T> scratchArc = new FST.Arc<>();

            {
                string stateColor;
                if (fst.IsExpandedTarget(startArc, r))
                {
                    stateColor = expandedNodeColor;
                }
                else
                {
                    stateColor = null;
                }

                bool isFinal;
                T finalOutput;
                if (startArc.Final)
                {
                    isFinal = true;
                    finalOutput = (object)startArc.NextFinalOutput == (object)NO_OUTPUT ? default(T) : startArc.NextFinalOutput;
                }
                else
                {
                    isFinal = false;
                    finalOutput = default(T);
                }

                EmitDotState(@out, Convert.ToString(startArc.Target), isFinal ? finalStateShape : stateShape, stateColor, finalOutput == null ? "" : fst.Outputs.OutputToString(finalOutput));
            }

            @out.Write("  initial -> " + startArc.Target + "\n");

            int level = 0;

            while (nextLevelQueue.Count > 0)
            {
                // we could double buffer here, but it doesn't matter probably.
                //System.out.println("next level=" + level);
                thisLevelQueue.AddRange(nextLevelQueue);
                nextLevelQueue.Clear();

                level++;
                @out.Write("\n  // Transitions and states at level: " + level + "\n");
                while (thisLevelQueue.Count > 0)
                {
                    FST<T>.Arc<T> arc = thisLevelQueue[thisLevelQueue.Count - 1];
                    thisLevelQueue.RemoveAt(thisLevelQueue.Count - 1);
                    //System.out.println("  pop: " + arc);
                    if (FST<T>.TargetHasArcs(arc))
                    {
                        // scan all target arcs
                        //System.out.println("  readFirstTarget...");

                        long node = arc.Target;

                        fst.ReadFirstRealTargetArc(arc.Target, arc, r);

                        //System.out.println("    firstTarget: " + arc);

                        while (true)
                        {
                            //System.out.println("  cycle arc=" + arc);
                            // Emit the unseen state and add it to the queue for the next level.
                            if (arc.Target >= 0 && !seen.Get((int)arc.Target))
                            {
                                /*
                                boolean isFinal = false;
                                T finalOutput = null;
                                fst.readFirstTargetArc(arc, scratchArc);
                                if (scratchArc.isFinal() && fst.targetHasArcs(scratchArc)) {
                                  // target is final
                                  isFinal = true;
                                  finalOutput = scratchArc.output == NO_OUTPUT ? null : scratchArc.output;
                                  System.out.println("dot hit final label=" + (char) scratchArc.label);
                                }
                                */
                                string stateColor;
                                if (fst.IsExpandedTarget(arc, r))
                                {
                                    stateColor = expandedNodeColor;
                                }
                                else
                                {
                                    stateColor = null;
                                }

                                string finalOutput;
                                if (arc.NextFinalOutput != null && (object)arc.NextFinalOutput != (object)NO_OUTPUT)
                                {
                                    finalOutput = fst.Outputs.OutputToString(arc.NextFinalOutput);
                                }
                                else
                                {
                                    finalOutput = "";
                                }

                                EmitDotState(@out, Convert.ToString(arc.Target), stateShape, stateColor, finalOutput);
                                // To see the node address, use this instead:
                                //emitDotState(out, Integer.toString(arc.target), stateShape, stateColor, String.valueOf(arc.target));
                                seen.Set((int)arc.Target, true);
                                nextLevelQueue.Add((new FST<T>.Arc<T>()).CopyFrom(arc));
                                sameLevelStates.Add((int)arc.Target);
                            }

                            string outs;
                            if ((object)arc.Output != (object)NO_OUTPUT)
                            {
                                outs = "/" + fst.Outputs.OutputToString(arc.Output);
                            }
                            else
                            {
                                outs = "";
                            }

                            if (!FST<T>.TargetHasArcs(arc) && arc.Final && (object)arc.NextFinalOutput != (object)NO_OUTPUT)
                            {
                                // Tricky special case: sometimes, due to
                                // pruning, the builder can [sillily] produce
                                // an FST with an arc into the final end state
                                // (-1) but also with a next final output; in
                                // this case we pull that output up onto this
                                // arc
                                outs = outs + "/[" + fst.Outputs.OutputToString(arc.NextFinalOutput) + "]";
                            }

                            string arcColor;
                            if (arc.Flag(FST<T>.BIT_TARGET_NEXT))
                            {
                                arcColor = "red";
                            }
                            else
                            {
                                arcColor = "black";
                            }

                            Debug.Assert(arc.Label != FST<T>.END_LABEL);
                            @out.Write("  " + node + " -> " + arc.Target + " [label=\"" + PrintableLabel(arc.Label) + outs + "\"" + (arc.Final ? " style=\"bold\"" : "") + " color=\"" + arcColor + "\"]\n");

                            // Break the loop if we're on the last arc of this state.
                            if (arc.Last)
                            {
                                //System.out.println("    break");
                                break;
                            }
                            fst.ReadNextRealArc(arc, r);
                        }
                    }
                }

                // Emit state ranking information.
                if (sameRank && sameLevelStates.Count > 1)
                {
                    @out.Write("  {rank=same; ");
                    foreach (int state in sameLevelStates)
                    {
                        @out.Write(state + "; ");
                    }
                    @out.Write(" }\n");
                }
                sameLevelStates.Clear();
            }

            // Emit terminating state (always there anyway).
            @out.Write("  -1 [style=filled, color=black, shape=doublecircle, label=\"\"]\n\n");
            @out.Write("  {rank=sink; -1 }\n");

            @out.Write("}\n");
            @out.Flush();
        }

        /// <summary>
        /// Emit a single state in the <code>dot</code> language.
        /// </summary>
        private static void EmitDotState(TextWriter @out, string name, string shape, string color, string label)
        {
            @out.Write("  " + name + " [" + (shape != null ? "shape=" + shape : "") + " " + (color != null ? "color=" + color : "") + " " + (label != null ? "label=\"" + label + "\"" : "label=\"\"") + " " + "]\n");
        }

        /// <summary>
        /// Ensures an arc's label is indeed printable (dot uses US-ASCII).
        /// </summary>
        private static string PrintableLabel(int label)
        {
            // Any ordinary ascii character, except for " or \, are
            // printed as the character; else, as a hex string:
            if (label >= 0x20 && label <= 0x7d && label != 0x22 && label != 0x5c) // " OR \
            {
                return char.ToString((char)label);
            }
            return "0x" + label.ToString("x");
        }

        /// <summary>
        /// Just maps each UTF16 unit (char) to the ints in an
        ///  IntsRef.
        /// </summary>
        public static IntsRef ToUTF16(string s, IntsRef scratch)
        {
            int charLimit = s.Length;
            scratch.Offset = 0;
            scratch.Length = charLimit;
            scratch.Grow(charLimit);
            for (int idx = 0; idx < charLimit; idx++)
            {
                scratch.Ints[idx] = (int)s[idx];
            }
            return scratch;
        }

        /// <summary>
        /// Decodes the Unicode codepoints from the provided
        ///  CharSequence and places them in the provided scratch
        ///  IntsRef, which must not be null, returning it.
        /// </summary>
        public static IntsRef ToUTF32(string s, IntsRef scratch)
        {
            int charIdx = 0;
            int intIdx = 0;
            int charLimit = s.Length;
            while (charIdx < charLimit)
            {
                scratch.Grow(intIdx + 1);
                int utf32 = Character.CodePointAt(s, charIdx);
                scratch.Ints[intIdx] = utf32;
                charIdx += Character.CharCount(utf32);
                intIdx++;
            }
            scratch.Length = intIdx;
            return scratch;
        }

        /// <summary>
        /// Decodes the Unicode codepoints from the provided
        ///  char[] and places them in the provided scratch
        ///  IntsRef, which must not be null, returning it.
        /// </summary>
        public static IntsRef ToUTF32(char[] s, int offset, int length, IntsRef scratch)
        {
            int charIdx = offset;
            int intIdx = 0;
            int charLimit = offset + length;
            while (charIdx < charLimit)
            {
                scratch.Grow(intIdx + 1);
                int utf32 = Character.CodePointAt(s, charIdx, charLimit);
                scratch.Ints[intIdx] = utf32;
                charIdx += Character.CharCount(utf32);
                intIdx++;
            }
            scratch.Length = intIdx;
            return scratch;
        }

        /// <summary>
        /// Just takes unsigned byte values from the BytesRef and
        ///  converts into an IntsRef.
        /// </summary>
        public static IntsRef ToIntsRef(BytesRef input, IntsRef scratch)
        {
            scratch.Grow(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                scratch.Ints[i] = input.Bytes[i + input.Offset] & 0xFF;
            }
            scratch.Length = input.Length;
            return scratch;
        }

        /// <summary>
        /// Just converts IntsRef to BytesRef; you must ensure the
        ///  int values fit into a byte.
        /// </summary>
        public static BytesRef ToBytesRef(IntsRef input, BytesRef scratch)
        {
            scratch.Grow(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                int value = input.Ints[i + input.Offset];
                // NOTE: we allow -128 to 255
                Debug.Assert(value >= sbyte.MinValue && value <= 255, "value " + value + " doesn't fit into byte");
                scratch.Bytes[i] = (sbyte)value;
            }
            scratch.Length = input.Length;
            return scratch;
        }

        // Uncomment for debugging:

        /*
        public static <T> void dotToFile(FST<T> fst, String filePath) throws IOException {
          Writer w = new OutputStreamWriter(new FileOutputStream(filePath));
          toDot(fst, w, true, true);
          w.close();
        }
        */

        /// <summary>
        /// Reads the first arc greater or equal that the given label into the provided
        /// arc in place and returns it iff found, otherwise return <code>null</code>.
        /// </summary>
        /// <param name="label"> the label to ceil on </param>
        /// <param name="fst"> the fst to operate on </param>
        /// <param name="follow"> the arc to follow reading the label from </param>
        /// <param name="arc"> the arc to read into in place </param>
        /// <param name="in"> the fst's <seealso cref="BytesReader"/> </param>
        public static FST<T>.Arc<T> readCeilArc<T>(int label, FST<T> fst, FST<T>.Arc<T> follow, FST<T>.Arc<T> arc, FST<T>.BytesReader @in)
        {
            // TODO maybe this is a useful in the FST class - we could simplify some other code like FSTEnum?
            if (label == FST<T>.END_LABEL)
            {
                if (follow.Final)
                {
                    if (follow.Target <= 0)
                    {
                        arc.Flags = (sbyte)FST<T>.BIT_LAST_ARC;
                    }
                    else
                    {
                        arc.Flags = 0;
                        // NOTE: nextArc is a node (not an address!) in this case:
                        arc.NextArc = follow.Target;
                        arc.Node = follow.Target;
                    }
                    arc.Output = follow.NextFinalOutput;
                    arc.Label = FST<T>.END_LABEL;
                    return arc;
                }
                else
                {
                    return null;
                }
            }

            if (!FST<T>.TargetHasArcs(follow))
            {
                return null;
            }
            fst.ReadFirstTargetArc(follow, arc, @in);
            if (arc.BytesPerArc != 0 && arc.Label != FST<T>.END_LABEL)
            {
                // Arcs are fixed array -- use binary search to find
                // the target.

                int low = arc.ArcIdx;
                int high = arc.NumArcs - 1;
                int mid = 0;
                // System.out.println("do arc array low=" + low + " high=" + high +
                // " targetLabel=" + targetLabel);
                while (low <= high)
                {
                    mid = (int)((uint)(low + high) >> 1);
                    @in.Position = arc.PosArcsStart;
                    @in.SkipBytes(arc.BytesPerArc * mid + 1);
                    int midLabel = fst.ReadLabel(@in);
                    int cmp = midLabel - label;
                    // System.out.println("  cycle low=" + low + " high=" + high + " mid=" +
                    // mid + " midLabel=" + midLabel + " cmp=" + cmp);
                    if (cmp < 0)
                    {
                        low = mid + 1;
                    }
                    else if (cmp > 0)
                    {
                        high = mid - 1;
                    }
                    else
                    {
                        arc.ArcIdx = mid - 1;
                        return fst.ReadNextRealArc(arc, @in);
                    }
                }
                if (low == arc.NumArcs)
                {
                    // DEAD END!
                    return null;
                }

                arc.ArcIdx = (low > high ? high : low);
                return fst.ReadNextRealArc(arc, @in);
            }

            // Linear scan
            fst.ReadFirstRealTargetArc(follow.Target, arc, @in);

            while (true)
            {
                // System.out.println("  non-bs cycle");
                // TODO: we should fix this code to not have to create
                // object for the output of every arc we scan... only
                // for the matching arc, if found
                if (arc.Label >= label)
                {
                    // System.out.println("    found!");
                    return arc;
                }
                else if (arc.Last)
                {
                    return null;
                }
                else
                {
                    fst.ReadNextRealArc(arc, @in);
                }
            }
        }
    }
}