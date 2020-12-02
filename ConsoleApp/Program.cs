using System;
using System.Reactive.Linq;
using static RxMethodGenerator.RxGeneratedMethods;
namespace ConsoleApp
{
    
    public class Program
    {
        public static void Main(string[] args)
        {
            var example = new  Example();
            example.RxActionEvent().Subscribe();
        }
    }

    public partial class Example
    {
        public event Action<int, string, bool> ActionEvent;
        public event Action<int, string, bool> ActionEvent1;

        public Example()
        {

           
        }

        public void Example1()
        {

        }
    }
}
