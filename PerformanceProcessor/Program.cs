using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using System.Net.Http;
using System.Net.Http.Headers;


using Newtonsoft.Json;

namespace PerformanceProcessor
{
    class Program
    {
        const string osuAPIKey = null;
        static HttpClient client = new HttpClient();

        static async Task Main(string[] args)
        {
            bool detailed = false;

            string[] users = { };
            int count = 100;
            if (args.Length > 0)
            {
                if (args[0] == "-h")
                {
                    Console.WriteLine("Calculates the pp value of the top 100 plays of selected users.");
                    return;
                }
                if (args[0] == "-d")
                {
                    Console.WriteLine("Returning detailed data.");
                    detailed = true;

                    users = new string[args.Length - 1];
                    for (int index = 0; index < args.Length - 1; index++)
                    {
                        users[index] = args[index + 1];
                    }
                }
                else
                {
                    users = args;
                }
            }

            if (users?.Length == 0)
            {
                Console.WriteLine("Please enter the users to read. Closing.");
                return;
            }
            else
            {
                string key = osuAPIKey;
                if (key == null)
                {
                    Console.WriteLine("Please input an osu api key. It will not be saved.");
                    Console.Write(" > ");
                    key = "k=" + Console.ReadLine();
                }

                //users is list of user names/ids, any failed should be ignored
                client.BaseAddress = new Uri("https://osu.ppy.sh/api/");
                client.DefaultRequestHeaders.Clear();

                List<Score> allUserScores = new List<Score>();

                Dictionary<int, string> userNames = new Dictionary<int, string>();

                Console.WriteLine("Loading user scores...");

                foreach (string user in users)
                {
                    IList<Score> userScores;
                    try
                    {
                        userScores = await GetUserTopScoresAsync(user, key);
                        if (userScores.Count > 0) //not found usernames should result in 0 size list
                        {
                            allUserScores.AddRange(userScores);
                            int userID = userScores[0].user_id;
                            string userName = await GetUserName(userID, key);

                            if (userName != null)
                            {
                                userNames.Add(userID, userName);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.Out.WriteLine("Error reading user \"" + user + "\"");
                    }
                }

                client.Dispose();

                //now we have the top 100 scores of all the specified users

                //next step - Load map database - use code from star rating comparison tool, load from local maps
                //Or, use full database. This is more of a pain, but it'll allow converts.

                string path = "";
                if (System.IO.Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\osu!\\Songs"))
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\osu!\\Songs";
                }
                else if (System.IO.Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\osu!\\Songs"))
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\osu!\\Songs";
                }
                else if (System.IO.Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + "\\osu!\\Songs"))
                {
                    path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + "\\osu!\\Songs";
                }
                else
                {
                    //prompt for folder
                    Console.WriteLine("osu! was not found in a default location. Please enter the full path to your osu folder.");
                    Console.Write(" > ");
                    path = Console.ReadLine();

                    while (!TestOsuFolder(path))
                    {
                        Console.WriteLine("Path failed test. Example: C:\\Program Files\\osu!");
                        Console.Write(" > ");
                        path = Console.ReadLine();
                    }

                    path += "\\Songs";
                }

                //now that we have osu folder location (hopefully) load map database
                Console.WriteLine();
                Console.WriteLine("Loading maps...");

                Dictionary<int, Beatmap> maps = LoadMaps(path);

                allUserScores.Sort((a, b) => a.beatmap_id.CompareTo(b.beatmap_id));

                //Go by beatmap id, to avoid loading single beatmaps multiple times
                

                //First, check for any missing maps

                int mapID = -1;
                List<int> missingIDs = new List<int>();

                foreach (Score s in allUserScores)
                {
                    if (s.beatmap_id != mapID)
                    {
                        mapID = s.beatmap_id;
                        if (!maps.ContainsKey(s.beatmap_id))
                        {
                            missingIDs.Add(s.beatmap_id);
                        }
                    }
                }

                if (missingIDs.Count > 0)
                {
                    Console.WriteLine(missingIDs.Count.ToString() + " maps were not found in your local beatmap collection or are not taiko maps. The map may also possibly be too old for this system to load properly. Listing IDs...");
                    int lineLength = 0;
                    foreach (int id in missingIDs)
                    {
                        allUserScores.RemoveAll(s => s.beatmap_id == id); //remove all scores that are missing the necessary map file

                        lineLength++;
                        Console.Write(id.ToString() + " ");
                        if (lineLength > 6)
                        {
                            Console.WriteLine();
                            lineLength = 0;
                        }
                    }
                    Console.WriteLine();
                    //Console.WriteLine("Would you like to continue anyways? (enter y/Y to continue, anything else will stop)");

                    //string input = Console.ReadLine();

                    //if (!input.ToLower().StartsWith("y"))
                    //{
                    //    return;
                    //}
                }

                Console.WriteLine("Beginning score processing.");
                try
                {
                    List<Play> plays = new List<Play>();

                    DifficultyCalculator d = new DifficultyCalculator();
                    double sr;
                    float oldpp;
                    double newpp;
                    double acc;
                    string modString;

                    foreach (Score s in allUserScores)
                    {
                        if (maps.ContainsKey(s.beatmap_id))
                        {
                            d.CalculateValue(s, maps[s.beatmap_id], out sr, out oldpp, out newpp, out acc, out modString);

                            plays.Add(new Play(userNames[s.user_id], maps[s.beatmap_id].ToString(), modString, sr, oldpp, newpp, acc, s.maxcombo));
                        }
                    }

                    plays.Sort((a, b) => b.newVal.CompareTo(a.newVal));

                    if (plays.Count > 0)
                    {
                        Console.WriteLine("Processing complete. If you would like to save the result to a file, please enter a filename. It will be saved on desktop.");
                        string filename = Console.ReadLine();


                        List<string> playStrings = new List<string>();
                        bool showUsernames = userNames.Count > 1;
                        foreach (Play p in plays)
                        {
                            playStrings.Add(p.ToString(showUsernames));
                            Console.WriteLine(p.ToString(showUsernames));
                        }
                        
                        if (filename.Length > 0)
                        {
                            if (!filename.EndsWith(".txt"))
                                filename += ".txt";

                            filename = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\" + filename;



                            System.IO.File.WriteAllLines(filename, playStrings);
                        }

                        Console.Write("Done.");
                        Console.ReadLine();
                    }
                    else
                    {
                        Console.WriteLine("No plays were found.");
                        return;
                    }

                }
                catch
                {
                    Console.WriteLine("Error during processing. Stopping.");
                    return;
                }
            }

        }

        static Dictionary<int, Beatmap> LoadMaps(string path)
        {
            Dictionary<int, Beatmap> AllMaps = new Dictionary<int, Beatmap>();
            
            int diffCount = 0;
            int calculatedCount = 0;
            int taikoMapCount = 0;

            try
            {
                string[] beatmapFiles = System.IO.Directory.GetFiles(path, "*.osu", System.IO.SearchOption.AllDirectories);
                diffCount = beatmapFiles.Length;
                Console.WriteLine("Found " + diffCount.ToString() + " possible .osu files; beginning to load maps");
                Console.WriteLine("____________________");

                double checkpoint = 0.05;

                foreach (string m in beatmapFiles)
                {
                    try
                    {
                        Beatmap map = Beatmap.Load(m);

                        if (map?.Mode == 1)
                        {
                            if (map.ID > -1)
                            {
                                if (!AllMaps.ContainsKey(map.ID))
                                {
                                    AllMaps.Add(map.ID, map); //does not calculate difficulty, only get metadata, id, and index of hitobjects in map for later reading
                                    taikoMapCount++;
                                }
                            }
                        }
                    }
                    catch
                    {
                        //failed to read one map
                    }
                    calculatedCount++;

                    if ((double)calculatedCount / diffCount > checkpoint)
                    {
                        checkpoint += 0.05;
                        Console.Write("=");
                    }
                }

                Console.WriteLine();
                Console.WriteLine("Found " + taikoMapCount.ToString() + " taiko maps. Beginning to calculate player pp scores.");
            }
            catch
            {
                Console.WriteLine("Error attempting to read maps.");
                return null;
            }

            return AllMaps;
        }

        static bool TestOsuFolder(string path)
        {
            if (System.IO.Directory.Exists(path))
            {
                if (System.IO.Directory.Exists(path + "\\Songs"))
                {
                    return true;
                }
            }
            return false;
        }

        static async Task<IList<Score>> GetUserTopScoresAsync(string user, string key)
        {
            HttpResponseMessage response = await client.GetAsync("get_user_best?u=" + user + "&m=1&limit=100&" + key);
            string dataString;

            if (response.IsSuccessStatusCode)
            {
                dataString = await response.Content.ReadAsStringAsync();
                IList<Score> scores = JsonConvert.DeserializeObject<IList<Score>>(dataString);
                return scores;
            }

            return null;
        }
        static async Task<string> GetUserName(int userID, string key)
        {
            HttpResponseMessage response = await client.GetAsync("get_user?u=" + userID.ToString() + "&type=id&" + key);
            string dataString;

            if (response.IsSuccessStatusCode)
            {
                dataString = await response.Content.ReadAsStringAsync();
                IList<User> user = JsonConvert.DeserializeObject<IList<User>>(dataString);
                if (user.Count > 0)
                    return user[0].username;
                return null;
            }

            return null;
        }
    }

    public class Score
    {
        public int beatmap_id { get; set; }
        public int score { get; set; }
        public int maxcombo { get; set; }
        public int count300 { get; set; }
        public int count100 { get; set; }
        public int count50 { get; set; }
        public int countmiss { get; set; }
        public int countkatu { get; set; }
        public int countgeki { get; set; }
        public int perfect { get; set; }
        public int enabled_mods { get; set; }
        public int user_id { get; set; }
        public DateTime date { get; set; }
        public RankType rank { get; set; }
        public float pp { get; set; }

        public enum RankType { A, B, C, D, S, SH, X, XH };
    }

    public class User
    {
        public int user_id { get; set; }
        public string username { get; set; }
        public DateTime join_date { get; set; }
        public int count300 { get; set; }
        public int count100 { get; set; }
        public int count50 { get; set; }
        public int playcount { get; set; }
        public long ranked_score { get; set; }
        public long total_score { get; set; }
        public int pp_rank { get; set; }
        public double level { get; set; }
        public double pp_raw { get; set; }
        public double accuracy { get; set; }
        public int count_rank_ss { get; set; }
        public int count_rank_ssh { get; set; }
        public int count_rank_s { get; set; }
        public int count_rank_sh { get; set; }
        public int count_rank_a { get; set; }
        public string country { get; set; }
        public long total_seconds_played { get; set; }
        public int pp_country_rank { get; set; }
        public IList<Event> events { get; set; }
    }

    public class Event
    {
        public string display_html { get; set; }
        public int beatmap_id { get; set; }
        public int beatmapset_id { get; set; }
        public DateTime date { get; set; }
        public int epicfactor { get; set; }
    }
}
