using System;
using System.Reactive.Linq;
using RxMethodGenerator;
namespace TestConsoleApp
{
    
    public class Program
    {
        public static void Main()
        {
            Example example = new Example();
            example.RxActionEvent().Subscribe();
            example.
        }
    }  

    public partial class Example
    {
        public event Action<int, string, bool> ActionEvent;
        public event Action<int, string, bool> ActionEvent1;
    }
}
