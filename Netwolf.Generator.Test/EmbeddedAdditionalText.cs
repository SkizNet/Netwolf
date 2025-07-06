using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Netwolf.Generator.Test;

internal class EmbeddedAdditionalText : AdditionalText
{
    private readonly string _path;

    public override string Path => _path;

    public EmbeddedAdditionalText(string path)
    {
        _path = path;
    }

    public override SourceText? GetText(CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream($"Netwolf.Generator.Test.{Path}")!);
        return new EmbeddedSourceText(reader.ReadToEnd());
    }

    private class EmbeddedSourceText : SourceText
    {
        private string Text { get; init; }

        public override char this[int position] => Text[position];

        public override Encoding? Encoding => Encoding.UTF8;

        public override int Length => Text.Length;

        public EmbeddedSourceText(string text)
        {
            Text = text;
        }

        public override void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count)
        {
            Text.CopyTo(sourceIndex, destination, destinationIndex, count);
        }
    }
}
