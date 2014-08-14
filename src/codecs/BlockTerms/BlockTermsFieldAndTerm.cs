using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lucene.Net.Util;

namespace Lucene.Net.Codecs.BlockTerms
{
    /// <summary>
    /// Used as key for the terms cache
    /// </summary>
    internal static class BlockTermsFieldAndTerm : DoubleBarrelLRUCache.CloneableKey
    {

    String field;
    BytesRef term;

    public FieldAndTerm() {
    }

    public FieldAndTerm(FieldAndTerm other) {
      field = other.field;
      term = BytesRef.deepCopyOf(other.term);
    }

    public override boolean equals(Object _other) {
      FieldAndTerm other = (FieldAndTerm) _other;
      return other.field.equals(field) && term.bytesEquals(other.term);
    }

    public override FieldAndTerm clone() {
      return new FieldAndTerm(this);
    }

    public override int hashCode() {
      return field.hashCode() * 31 + term.hashCode();
    }
  }

    }
}
