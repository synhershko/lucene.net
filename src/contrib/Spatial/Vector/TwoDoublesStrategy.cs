﻿/*
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

using System;
using Lucene.Net.Documents;
using Lucene.Net.Search;
using Lucene.Net.Search.Function;
using Lucene.Net.Spatial.Util;
using Spatial4n.Core.Context;
using Spatial4n.Core.Exceptions;
using Spatial4n.Core.Query;
using Spatial4n.Core.Shapes;

namespace Lucene.Net.Spatial.Vector
{
	public class TwoDoublesStrategy : SpatialStrategy<TwoDoublesFieldInfo>
	{
		private readonly NumericFieldInfo finfo;
		private readonly DoubleParser parser;

		public TwoDoublesStrategy(SpatialContext ctx, NumericFieldInfo finfo, DoubleParser parser)
			: base(ctx)
		{
			this.finfo = finfo;
			this.parser = parser;
		}

		public override bool IsPolyField()
		{
			return true;
		}

		public override Field[] CreateFields(TwoDoublesFieldInfo fieldInfo, Shape shape, bool index, bool store)
		{
			if (shape is Point)
			{
				Point point = (Point)shape;

				Field[] f = new Field[(index ? 2 : 0) + (store ? 1 : 0)];
				if (index)
				{
					f[0] = finfo.CreateDouble(fieldInfo.GetFieldNameX(), point.GetX());
					f[1] = finfo.CreateDouble(fieldInfo.GetFieldNameY(), point.GetY());
				}
				if (store)
				{
					FieldType customType = new FieldType();
					customType.SetStored(true);
					f[f.Length - 1] = new Field(fieldInfo.GetFieldName(), ctx.ToString(shape), customType);
				}
				return f;
			}
			if (!ignoreIncompatibleGeometry)
			{
				throw new ArgumentException("TwoDoublesStrategy can not index: " + shape);
			}
			return new Field[0]; // nothing (solr does not support null) 
		}

		public override Field CreateField(TwoDoublesFieldInfo fieldInfo, Shape shape, bool index, bool store)
		{
			throw new InvalidOperationException("Point is poly field");
		}

		public override ValueSource MakeValueSource(SpatialArgs args, TwoDoublesFieldInfo fieldInfo)
		{
			Point p = args.GetShape().GetCenter();
			return new DistanceValueSource(p, ctx.GetDistCalc(), fieldInfo, parser);
		}

		public override Query MakeQuery(SpatialArgs args, TwoDoublesFieldInfo fieldInfo)
		{
			// For starters, just limit the bbox
			Shape shape = args.GetShape();
			if (!(shape is Rectangle))
			{
				throw new InvalidShapeException("A rectangle is the only supported shape (so far), not " + shape.GetType().Name);//TODO
			}
			Rectangle bbox = (Rectangle)shape;
			if (bbox.GetCrossesDateLine())
			{
				throw new InvalidOperationException("Crossing dateline not yet supported");
			}

			ValueSource valueSource = null;

			Query spatial = null;
			SpatialOperation op = args.Operation;

			if (SpatialOperation.Is(op,
				SpatialOperation.BBoxWithin,
				SpatialOperation.BBoxIntersects))
			{
				spatial = MakeWithin(bbox, fieldInfo);
			}
			else if (SpatialOperation.Is(op,
			  SpatialOperation.Intersects,
			  SpatialOperation.IsWithin))
			{
				spatial = MakeWithin(bbox, fieldInfo);
				if (args.GetShape() is Circle)
				{
					Circle circle = (Circle)args.GetShape();

					// Make the ValueSource
					valueSource = MakeValueSource(args, fieldInfo);

					ValueSourceFilter vsf = new ValueSourceFilter(
						new QueryWrapperFilter(spatial), valueSource, 0, circle.GetDistance());

					spatial = new FilteredQuery(new MatchAllDocsQuery(), vsf);
				}
			}
			else if (op == SpatialOperation.IsDisjointTo)
			{
				spatial = MakeDisjoint(bbox, fieldInfo);
			}

			if (spatial == null)
			{
				throw new UnsupportedSpatialOperation(args.Operation);
			}

			if (valueSource != null)
			{
				valueSource = new CachingDoubleValueSource(valueSource);
			}
			else
			{
				valueSource = MakeValueSource(args, fieldInfo);
			}
			Query spatialRankingQuery = new FunctionQuery(valueSource);
			BooleanQuery bq = new BooleanQuery();
			bq.Add(spatial, BooleanClause.Occur.MUST);
			bq.Add(spatialRankingQuery, BooleanClause.Occur.MUST);
			return bq;

		}

		public override Filter MakeFilter(SpatialArgs args, TwoDoublesFieldInfo fieldInfo)
		{
			if (args.GetShape() is Circle)
			{
				if (SpatialOperation.Is(args.Operation,
					SpatialOperation.Intersects,
					SpatialOperation.IsWithin))
				{
					Circle circle = (Circle)args.GetShape();
					Query bbox = MakeWithin(circle.GetBoundingBox(), fieldInfo);

					// Make the ValueSource
					ValueSource valueSource = MakeValueSource(args, fieldInfo);

					return new ValueSourceFilter(
						new QueryWrapperFilter(bbox), valueSource, 0, circle.GetDistance());
				}
			}
			return new QueryWrapperFilter(MakeQuery(args, fieldInfo));

		}

		/// <summary>
		/// Constructs a query to retrieve documents that fully contain the input envelope.
		/// </summary>
		/// <param name="bbox"></param>
		/// <param name="fieldInfo"></param>
		/// <returns>The spatial query</returns>
		private Query MakeWithin(Rectangle bbox, TwoDoublesFieldInfo fieldInfo)
		{
			Query qX = NumericRangeQuery.NewDoubleRange(
			  fieldInfo.GetFieldNameX(),
			  finfo.precisionStep,
			  bbox.GetMinX(),
			  bbox.GetMaxX(),
			  true,
			  true);
			Query qY = NumericRangeQuery.NewDoubleRange(
			  fieldInfo.GetFieldNameY(),
			  finfo.precisionStep,
			  bbox.GetMinY(),
			  bbox.GetMaxY(),
			  true,
			  true);

			BooleanQuery bq = new BooleanQuery();
			bq.Add(qX, BooleanClause.Occur.MUST);
			bq.Add(qY, BooleanClause.Occur.MUST);
			return bq;
		}

		/// <summary>
		/// Constructs a query to retrieve documents that fully contain the input envelope.
		/// </summary>
		/// <param name="bbox"></param>
		/// <param name="fieldInfo"></param>
		/// <returns>The spatial query</returns>
		Query MakeDisjoint(Rectangle bbox, TwoDoublesFieldInfo fieldInfo)
		{
			Query qX = NumericRangeQuery.NewDoubleRange(
			  fieldInfo.GetFieldNameX(),
			  finfo.precisionStep,
			  bbox.GetMinX(),
			  bbox.GetMaxX(),
			  true,
			  true);
			Query qY = NumericRangeQuery.NewDoubleRange(
			  fieldInfo.GetFieldNameY(),
			  finfo.precisionStep,
			  bbox.GetMinY(),
			  bbox.GetMaxY(),
			  true,
			  true);

			BooleanQuery bq = new BooleanQuery();
			bq.Add(qX, BooleanClause.Occur.MUST_NOT);
			bq.Add(qY, BooleanClause.Occur.MUST_NOT);
			return bq;
		}

	}
}
