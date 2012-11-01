using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SpeedyDelete.zuora;
using System.Threading;

namespace SpeedyDelete
{
    class Program
    {

        String username = "";
        String password = "";

        String ENDPOINT = "https://apisandbox.zuora.com/apps/services/a/38.0";
        private zuora.ZuoraService binding;
        String querystring = "SELECT id from account";
        String delObject = "account";
        //
        //Vars
        //
        int numThreads = 64;
        int numToDelete = 5;
        DateTime dt = new DateTime(2012, 6, 11);
        //string querystring;

        
        QueryResult qResult;

        Boolean isMore = false;

        DateTime starttime = DateTime.Now;
        TimeSpan elapsedTime;

        int deleted = 0;

        //
        //thread vars
        //
        int threadsInUse = 0;
        
        List<ManualResetEvent> locks;


        public Program()
        {
            
            binding = new zuora.ZuoraService();
            binding.Url = ENDPOINT;
            binding.Timeout = 600000;

            Console.WriteLine("Enter user name: ");
            username = Console.ReadLine();

            Console.WriteLine("Enter password: ");
            password = Console.ReadLine();

            Console.WriteLine("Enter query string: ");
            querystring = Console.ReadLine();

            Console.WriteLine("Enter Object thats being deleted");
            delObject = Console.ReadLine();

            Console.WriteLine("Production or Sandbox? Enter P or S");
            String pors = Console.ReadLine();
            if (pors == "S")
            {
                ENDPOINT = "https://apisandbox.zuora.com/apps/services/a/38.0";
            }
            else if(pors == "P")
            {
                ENDPOINT = "https://www.zuora.com/apps/services/a/38.0";
            }

            qResult = new QueryResult();
        }
        //
        //kicks of the delete, it keeps querying till there is no more to query
        //it deletes the first query then if there is more does more...
        //
        public void startDelete()
        {
            login();
            zObject[] queryRes = doQuery();

            Console.WriteLine("Size: " + queryRes.Length);
            Console.WriteLine("Press Enter to Continue..." + queryRes.Length);
            Console.ReadKey();
            
            handleQuery(queryRes);
            //
            //read after first 2k
            //
            //Console.ReadKey();
            //
            //if theres more do them
            //
            while (isMore == true)
            {
                queryRes = doQuery();
                handleQuery(queryRes);
                //Console.ReadKey();
            }
        }
       
        //
        //delete for multithread
        //
        public void deleteList(object state)
        {
            threadsInUse++;
           //String[] temparray;
            object[] triplet = (object[])state;

            ManualResetEvent evt = (ManualResetEvent)triplet[1];
            List<string> delMe = (List<string>)triplet[0]; 
            
            DateTime delStartTime = DateTime.Now;
            //
            //do the delete
            //
            bool delResult = delete(delObject, delMe.ToArray());

            Console.WriteLine(delResult);
            Console.WriteLine("Elapsed in del: " + (DateTime.Now - delStartTime));
            elapsedTime = DateTime.Now - starttime;
            Console.WriteLine("Total Elapsed: " + elapsedTime + " Total Deleted " + deleted +  " deletes/second: " + (deleted/elapsedTime.TotalSeconds));
            evt.Set();
            threadsInUse--;
        }

        //
        //given any size query handles the breaking it into numToDelete and handiling the numThreads
        //
        public void handleQuery(zObject[] objs)
        {
            zObject[] temp = objs;
            int size = temp.Length;
            
            List<string> ids = new List<string> { };

            locks = new List<ManualResetEvent>();
            ManualResetEvent evt;
            ThreadPool.SetMaxThreads(numThreads, numThreads);
            ThreadPool.SetMinThreads(1, 1);
            //
            //make lists not greater than NumToDelete and then delete them and reset the list
            //
            for(int i = 0; i < temp.Length; i++)
            {
                //Console.WriteLine("adding id: " + temp[i].Id);
                ids.Add(temp[i].Id);
                //
                //delte when ids.Count == numToDelete and there is a thread ready
                //
                if (ids.Count == numToDelete || i==temp.Length-1)
                {
                    Console.WriteLine("Delete" + i);
                    evt = new ManualResetEvent(false);
                    ThreadPool.QueueUserWorkItem(new WaitCallback(deleteList), (object)(new object[] { ids, evt }));
                    locks.Add(evt);

                    //Console.ReadKey();
                    int worker;
                    int async;
                    ThreadPool.GetAvailableThreads(out worker, out async);
                    //Console.WriteLine("Avail workers:" + worker);
                    if (locks.Count >= numThreads || worker == 0)
                    {

                        Console.WriteLine("Waiting for threads to finish" + " " + DateTime.Now.TimeOfDay);
                        EventWaitHandle.WaitAll(locks.ToArray());
                        Thread.Sleep(5);
                        ThreadPool.GetAvailableThreads(out worker, out async);
                        Console.WriteLine(" Number of Threads: " + numThreads + " " + DateTime.Now.TimeOfDay);
                        //Console.ReadKey();
                        locks = new List<ManualResetEvent> { };
                    }

                    ids = new List<string> { };
                }

                              
            }


        }
        static void Main(string[] args)
        {
            Program p = new Program();
            p.startDelete();
            Console.WriteLine("Done...");
            Console.ReadKey();
        }
        public zObject[] doQuery()
        {
            
            if (isMore == false)
            {
                qResult = binding.query(querystring);
                //Console.WriteLine("Size: "+qResult.size);
            }
            else
            {
                qResult = binding.queryMore(qResult.queryLocator);
            }
            if (!qResult.done)
            {
                isMore = true;
            }
            else
            {
                isMore = false;
            }   
            
            return (zObject[]) qResult.records;
            

            
        }

        //login
        private bool login()
        {

            try
            {
                //execute the login placing the results  
                //in a LoginResult object 
                zuora.LoginResult loginResult = binding.login(username, password);

                //set the session id header for subsequent calls 
                binding.SessionHeaderValue = new zuora.SessionHeader();
                binding.SessionHeaderValue.session = loginResult.Session;

                //reset the endpoint url to that returned from login 
                // binding.Url = loginResult.ServerUrl;

                Console.WriteLine("Session: " + loginResult.Session);
                Console.WriteLine("ServerUrl: " + loginResult.ServerUrl);

                return true;
            }
            catch (Exception ex)
            {
                //Login failed, report message then return false 
                Console.WriteLine("Login failed with message: " + ex.Message);
                return false;
            }
        }
        private string create(zObject acc)
        {
            SaveResult[] result = binding.create(new zObject[] { acc });
            return result[0].Id;
        }

        private Account queryAccount(string accId)
        {
            QueryResult qResult = binding.query("SELECT id FROM account WHERE id = '" + accId + "'");
            Account rec = (Account)qResult.records[0];
            return rec;
        }

        private string update(zObject acc)
        {
            SaveResult[] result = binding.update(new zObject[] { acc });
            return result[0].Id;
        }

        private bool delete(String type, string[] ids)
        {           
            DeleteResult[] result = binding.delete(type, ids);
            for (int i = 0; i < result.Length; i++)
            {
                DeleteResult dr = result[i];
                if (dr.success == false)
                {
                    for (int j = 0; j < dr.errors.Length; j++)
                    {
                        Console.WriteLine(dr.errors[j].Message);
                        return false;
                    }
                }
                else
                {
                    deleted ++;
                }
            }
            
            
            return true;
            
        }
    }
}
