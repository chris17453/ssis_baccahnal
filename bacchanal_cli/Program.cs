using System;
using bacchanal;

namespace bacchanal_cli {
    class Program {
        static void Main(string[] args) {
            table_translator t = new table_translator(args[0]);
        }
    }
}
