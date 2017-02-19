using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Timers;
using Timer = System.Timers.Timer;
namespace AsyncTimer
{
    class Program
    {
        public static Timer timer = new Timer(5000);
        private static BroadcastBlock<DateTime> queue;
        private static CancellationToken token;
        private static CancellationTokenSource cancellationTokenSource;

        static void Main(string[] args)
        {
            cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(100));
            token = cancellationTokenSource.Token;
            Timer timer = new Timer(1);
            timer.AutoReset = true;
            queue = new BroadcastBlock<DateTime>((f) => { return f; }, new DataflowBlockOptions {  MaxMessagesPerTask= 100, BoundedCapacity = 1, CancellationToken = token,});


            try
            {
                var task = Task.Run(async () =>
              {
                  // Define the mesh.

                  timer.Elapsed += async (sender, e) =>
                {
                    await queue.SendAsync<DateTime>(e.SignalTime);
                    //queue.Complete();
                };
                  var consumerOptions = new ExecutionDataflowBlockOptions { BoundedCapacity = 1, MaxDegreeOfParallelism = Environment.ProcessorCount };
                  var linkOptions = new DataflowLinkOptions { PropagateCompletion = true, };


                  List<ActionBlock<DateTime>> actionblocks = new List<ActionBlock<DateTime>>();
               




                  for (int i = 0; i < 100; i++)
                  {
                      var currentNumber = i;
                      var consumer1 = new ActionBlock<DateTime>((date) => { Console.WriteLine($"consumer {currentNumber}: Date: { date:hh:mm:ss:ffff}"); }, consumerOptions);

                      actionblocks.Add(consumer1);

                      queue.LinkTo(consumer1, linkOptions);

                  }



                  // Wait for everything to complete.
                  try
                  {
                      List<Task> tasks = actionblocks.Select(s => s.Completion).ToList();

                      tasks.Add(queue.Completion);

                      tasks.Add(Task.Delay(TimeSpan.FromMilliseconds(-1), token));

                      await Task.WhenAny(tasks);

                      queue.Complete();

                     // token.ThrowIfCancellationRequested();
                
                  }
                  catch (Exception ex)
                  {

                      throw;
                  }

              }, token);

                timer.Start();

                task.ContinueWith((taskResult) => { }, TaskContinuationOptions.OnlyOnFaulted);


                task.Wait();

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.InnerException.Message);
            }



            Console.Write("Press any key to exit... ");

            Console.ReadKey();
        }

        private static async Task Produce(BufferBlock<DateTime> queue, DateTime time)
        {
            await queue.SendAsync(time);

        }
        private static async Task Consume(BufferBlock<DateTime> queue)
        {
            while (await queue.OutputAvailableAsync() && !token.IsCancellationRequested)
            {
                try
                {
                    Console.WriteLine(await queue.ReceiveAsync());
                }
                catch (Exception)
                {

                }
            }
            queue.Complete();
            token.ThrowIfCancellationRequested();
        }

        private static async Task<string> HandleTimer(object sender, ElapsedEventArgs e)
        {

            await queue.SendAsync(e.SignalTime);

            var result = e.SignalTime.ToLongTimeString();

            var random = new Random().Next(0, 150);


            await Task.Delay(TimeSpan.FromMilliseconds(random));

            Console.WriteLine(e.SignalTime.ToLongTimeString());

            return result;
        }
    }

}
