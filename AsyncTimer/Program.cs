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
            cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(10));
            token = cancellationTokenSource.Token;
            Timer timer = new Timer(10);
            timer.AutoReset = true;
            queue = new BroadcastBlock<DateTime>( (s) => { return s; }, new DataflowBlockOptions { BoundedCapacity = 5, });


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
                  var consumerOptions = new ExecutionDataflowBlockOptions { BoundedCapacity = 1, };

                  var consumer1 = new ActionBlock<DateTime>((date) => { Console.WriteLine($"consumer 1: Date: { date:hh:mm:ss:fff}"); } , consumerOptions);
                  var consumer2 = new ActionBlock<DateTime>((date) => { Console.WriteLine($"consumer 2: Date: { date:hh:mm:ss:fff}"); } , consumerOptions);
                  var consumer3 = new ActionBlock<DateTime>((date) => { Console.WriteLine($"consumer 3: Date: { date:hh:mm:ss:fff}"); } , consumerOptions);
                  var consumer4 = new ActionBlock<DateTime>((date) => { Console.WriteLine($"consumer 4: Date: { date:hh:mm:ss:fff}"); } , consumerOptions);
                  // Start the producer and consumer.

                  var linkOptions = new DataflowLinkOptions { PropagateCompletion = true, };
                  queue.LinkTo(consumer1, linkOptions);
                  queue.LinkTo(consumer2, linkOptions);
                  queue.LinkTo(consumer3, linkOptions);
                  queue.LinkTo(consumer4, linkOptions);


                  // Wait for everything to complete.
                  try
                  {

                      await Task.WhenAny(queue.Completion, consumer1.Completion, consumer2.Completion, consumer3.Completion, consumer4.Completion, Task.Delay(TimeSpan.FromMilliseconds(-1),token));

                      queue.Complete();

                      token.ThrowIfCancellationRequested();
                
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
