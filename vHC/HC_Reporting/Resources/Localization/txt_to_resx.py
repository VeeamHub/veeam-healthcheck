#!/usr/bin/env python3
import argparse
from xml.sax.saxutils import escape as xml_escape

RESX_HEADER = """<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
"""

RESX_FOOTER = "</root>\n"

def unescape_basic(s: str) -> str:
    # Turn common \" \n \\ sequences into real characters (optional).
    s = s.replace("\\\\", "\\")
    s = s.replace(r"\r", "\r").replace(r"\n", "\n").replace(r"\t", "\t")
    s = s.replace(r"\"", "\"")
    return s

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("input_txt")
    ap.add_argument("output_resx")
    ap.add_argument("--unescape", action="store_true",
                    help=r'Convert sequences like \" \n \\ into real characters')
    args = ap.parse_args()

    items = []
    with open(args.input_txt, "r", encoding="utf-8") as f:
        for raw in f:
            line = raw.strip()
            if not line or line.startswith("#"):
                continue
            if "=" not in line:
                continue

            key, value = line.split("=", 1)
            key = key.strip()
            value = value.strip()

            if args.unescape:
                value = unescape_basic(value)

            items.append((key, value))

    with open(args.output_resx, "w", encoding="utf-8", newline="\n") as out:
        out.write(RESX_HEADER)
        for k, v in items:
            # xml_escape will correctly handle:
            # - <a ...>  -> &lt;a ...&gt; in the .resx file (and becomes <a ...> at runtime)
            # - &amp; in your strings becomes &amp;amp; in the file (and becomes &amp; at runtime)
            out.write(f'  <data name="{xml_escape(k)}" xml:space="preserve">\n')
            out.write(f"    <value>{xml_escape(v)}</value>\n")
            out.write("  </data>\n")
        out.write(RESX_FOOTER)

if __name__ == "__main__":
    main()