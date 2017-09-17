using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;

namespace TactusDataAnalytics
{
    //The Application class does all the work.
    public class Application
    {
        //This list of files stores all the data for all rounds - each File contains several Rounds
        //Each Round has several Events
        List<FileOfRounds> _ListOfFiles;

        ///////////////////////////////////////////////////////////////////
        /// READ FILES ETC ////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////

        public Application()
        {
            _ListOfFiles = new List<FileOfRounds>();
        }

        /// <summary>
        /// Write a single line to the console
        /// </summary>
        /// <param name="s"></param>
        void WriteToConsole(string s)
        {
            Console.Write(s + "\n");
        }

        /// <summary>
        /// Read in the list of files from the given folder
        /// </summary>
        /// <param name="foldername"></param>
        public void ReadFileList(string foldername)
        {
            //for each file in the folder, read it in and add it to the list
            foreach (string filename in Directory.EnumerateFiles(foldername, "*.xml"))
            {
                _ListOfFiles.Add(ReadFile(filename));
            }
        }

        /// <summary>
        /// Get the session ID out of a Filename string, formatted in the way that Tactus outputs them.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public string GetSessionID(string filename)
        {
            //Break the string
            Char[] delimiter = new Char[] { '\\', 'S', 'T' };
            String[] substrings = filename.Split(delimiter);

            //for each string in the list of substrings, if it starts with ID save it as the ID
            foreach (string s in substrings)
            {
                if (s.Contains("ID"))
                {
                    WriteToConsole("Session ID = " + s);
                    return s;
                }
            }
            //otherwise write that none was found
            WriteToConsole("No sessionID");
            return "NOID";
        }

        /// <summary>
        /// Get the time uploaded from a filename string, formatted in the way that Tactus outputs them.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public DateTime GetSessionUploadTime(string filename)
        {
            //as before split the string
            Char[] delimiter = new Char[] { '_', '.' };
            String[] substrings = filename.Split(delimiter);

            //create a new DateTime at 1 year, 1 month, 1 day, and the time saved in the substrings.
            return new DateTime(1, 1, 1, int.Parse(substrings[1]), int.Parse(substrings[2]), int.Parse(substrings[3]), 1);
        }

        /// <summary>
        /// Open a file and read in the rounds in it.
        /// This assumes files formatted in XML the way Tactus outputs them.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public FileOfRounds ReadFile(string filename)
        {

            WriteToConsole("Reading file " + filename);

            //get the file data
            FileOfRounds f = new FileOfRounds();
            f.sessionID = GetSessionID(filename);
            f._uploadTime = GetSessionUploadTime(filename);
            f._roundsInFile = new List<RoundOfEvents>();

            //open an xml text reader
            XmlTextReader reader = new XmlTextReader(filename);

            //this reads one XML node at a time until it finds the start of a round, when ReadRound takes over
            while (reader.Read())
            {
                //for each line
                //if that line is the start of a round, read that round until it ends
                if (reader.NodeType == XmlNodeType.Element && (reader.Name == "Tap" || reader.Name == "Swipe" || reader.Name == "Tilt"))
                {
                    f.fileTag = (Program.ControlScheme)Enum.Parse(typeof(Program.ControlScheme), reader.Name);
                    WriteToConsole("Reading " + reader.Name + " round.");

                    RoundOfEvents r = ReadRound(reader, reader.Name, f.sessionID);
                    if (r != null) { f._roundsInFile.Add(r); }

                }

                //alternatively, it might be the start of the responses, in which case read them in
                else if (reader.NodeType == XmlNodeType.Element && reader.Name == "ResponseScore")
                {
                    ReadResponses(reader, f);
                }
                else //this means we're not reading the round start and there's something before it. keep reading until we do read a round start
                {
                    //WriteToConsole("Wrong XML Node Type, expecting Round Start, got "+reader.Name);
                }
                //if not, something is broken
            }
            return f;
        }

        /// <summary>
        /// Read in a round of events
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="controls"></param>
        /// <param name="sessionID"></param>
        /// <returns></returns>
        RoundOfEvents ReadRound(XmlReader reader, string controls, string sessionID)
        {
            //get thr round info
            RoundOfEvents r = new RoundOfEvents();
            r.roundTag = (Program.ControlScheme)Enum.Parse(typeof(Program.ControlScheme), controls);
            r.sessionID = sessionID;
            r._eventsInRound = new List<Event>();

            //as before, this reads one node at a time
            while (reader.Read())
            {
                //the first line of this should be the start of the first event of the round
                if (reader.NodeType == XmlNodeType.Element)
                {
                    //if it is in fact the first node of one of the Tap elements
                    if (reader.Name == "time")
                    {
                        //then this isn't a round! It's a Tap event from before the start of the round!
                        //this is a hacky workaround to deal with the Tap event and Tap mode being called the same thing
                        WriteToConsole("Not actually a round!");

                        //keep reading until the end of this event and then return null - the ReadFile discards a null result
                        while (reader.Read())
                        {
                            if (reader.NodeType == XmlNodeType.EndElement && (reader.Name == "Tap"))
                            {
                                return null;
                            }
                        }
                    }

                    //if it's not broken, read in the event!
                    r._eventsInRound.Add(ReadEvent(reader, reader.Name));
                }
                //if it's an end element it should be the end of the round
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    //this should be the end of the round

                    //depending on what the end element actually is, set the round tag
                    switch (reader.Name)
                    {
                        case "Tap":
                            r.roundTag = Program.ControlScheme.Tap;
                            return r;

                        case "Tilt":
                            r.roundTag = Program.ControlScheme.Tilt;
                            return r;

                        case "Swipe":
                            r.roundTag = Program.ControlScheme.Swipe;
                            return r;

                        default:
                            WriteToConsole("XXXXXXXX Something is wrong! Unexpected element end!");
                            break;
                    }
                }
                else
                {
                    WriteToConsole("XXXXXXXX Something is wrong! Wrong XML Node Type, expecting Event Start, got " + reader.Value);
                }
            }

            return r; //return the finished R.
        }

        /// <summary>
        /// Read in a single event
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        Event ReadEvent(XmlReader reader, string tag)
        {
            Event e = new Event();

            string s = "blank";

            //read in each node
            while (reader.Read())
            {
                //if its the end of the event, set the tag and return the event
                if (reader.NodeType == XmlNodeType.EndElement && reader.Name == tag)
                {
                    try
                    {
                        e._tag = (Program.EventTag)Enum.Parse(typeof(Program.EventTag), tag);
                    }
                    catch (Exception ex) { }
                    return e;
                }

                //if its the middle of an element, store the text
                if (reader.NodeType == XmlNodeType.Text)
                {
                    s = reader.Value;
                }

                //if it's the end of an element, save the stored text as the relevant thing
                if (reader.NodeType == XmlNodeType.EndElement)
                {
                    switch (reader.Name)
                    {
                        case "time":
                            e._time = float.Parse(s);
                            break;

                        case "currentLocation":
                            e._currentLocation = s;
                            break;

                        case "tapLocation":
                            e._tapLocation = s;
                            break;

                        case "currentDirection":
                            e._currentDirection = s;
                            break;

                        case "newDirection":
                            e._newDirection = s;
                            break;

                        case "successfulChange":
                            e._succeedChanging = bool.Parse(s);
                            break;

                        case "newHealth":
                            e._newHealth = int.Parse(s);
                            break;

                        case "damageSource":
                            e._damageSource = (Program.DamageSource)Enum.Parse(typeof(Program.DamageSource), s);
                            break;

                        case "endScore":
                            e._score = int.Parse(s);
                            break;

                        case "endHealth":
                            e._healthLeft = int.Parse(s);
                            break;

                        default:
                            WriteToConsole("Broken reading in event");
                            break;

                    }
                }
            }
            return new Event();
            //gather the event data and return it to the list of events in the round
        }

        /// <summary>
        /// Read the response scores from the player ratings event and store them.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="f"></param>
        public void ReadResponses(XmlReader reader, FileOfRounds f)
        {
            int num = 0;
            while (reader.Read())
            {
                //if we're at the end of the responses, return
                if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "ResponseScore")
                {
                    return;
                }
                //otherwise do the same as before - save the text
                else if (reader.NodeType == XmlNodeType.Text)
                {
                    num = int.Parse(reader.Value);
                }
                //and if we're at the end of the element
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    switch (reader.Name)
                    {
                        case "Control":
                            f._control = num;
                            break;

                        case "Challenge":
                            f._challenge = num;
                            break;

                        case "Fun":
                            f._fun = num;
                            break;
                        default:
                            WriteToConsole("Broken in reading responses");
                            break;

                    }
                }
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////// ANALYSING DATA ///////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////

        //note that a lot of these methods do similar things: i'll be only commenting or new or unusual things

        /// <summary>
        /// Count the number of rounds in all the files
        /// </summary>
        public void CountRounds()
        {
            int i = 0;
            int iTap = 0;
            int iTilt = 0;
            int iSwipe = 0;

            //for each file, for each round in each file
            foreach (FileOfRounds f in _ListOfFiles)
            {
                foreach (RoundOfEvents r in f._roundsInFile)
                {
                    i++;
                    switch (r.roundTag)
                    {
                        case Program.ControlScheme.Swipe:
                            iSwipe++;
                            break;
                        case Program.ControlScheme.Tap:
                            iTap++;
                            break;
                        case Program.ControlScheme.Tilt:
                            iTilt++;
                            break;
                        default:
                            break;
                    }
                }
            }
            WriteToConsole("Total Rounds: " + i + " Swipe: " + iSwipe + " Tap: " + iTap + " Tilt: " + iTilt);
        }


        /// <summary>
        /// Get the average Control rating for all the files
        /// </summary>
        public void AverageRatingControl()
        {
            //get the total score for each mode and the number of files surveyed for each mode
            float i = 0, iTotal = 0;
            float iTap = 0, iTapTotal = 0;
            float iTilt = 0, iTiltTotal = 0;
            float iSwipe = 0, iSwipeTotal = 0;
            foreach (FileOfRounds f in _ListOfFiles)
            {
                float s = f._control;
                if (s != 0)
                {
                    i++;
                    iTotal = iTotal + s;
                    switch (f.fileTag)
                    {
                        case Program.ControlScheme.Swipe:
                            iSwipe++;
                            iSwipeTotal = iSwipeTotal + s;
                            break;
                        case Program.ControlScheme.Tap:
                            iTap++;
                            iTapTotal = iTapTotal + s;
                            break;
                        case Program.ControlScheme.Tilt:
                            iTilt++;
                            iTiltTotal = iTiltTotal + s;
                            break;
                        default:
                            break;
                    }
                }

            }
            //divide them to get the averages and print
            WriteToConsole("Average Control score: Overall: " + iTotal / i + " Swipe: " + iSwipeTotal / iSwipe + " Tap: " + iTapTotal / iTap + " Tilt: " + iTiltTotal / iTilt);
        }

        /// <summary>
        /// Count the number of Sessions for each control scheme
        /// </summary>
        public void CountSessions()
        {
            //create a list of session IDs for each game mode
            List<string> idList = new List<string>();
            List<string> idListSwipe = new List<string>();
            List<string> idListTap = new List<string>();
            List<string> idListTilt = new List<string>();
            List<string> idListAll = new List<string>();

            //for each file, if the session ID is not already in a relevant list, add it
            //so a player can play Tap mode twice and only be counted once, for instance.
            foreach (FileOfRounds f in _ListOfFiles)
            {

                if (!idList.Contains(f.sessionID))
                {
                    idList.Add(f.sessionID);
                }
                if (f.fileTag == Program.ControlScheme.Tilt && !idListTilt.Contains(f.sessionID))
                {
                    idListTilt.Add(f.sessionID);
                }
                if (f.fileTag == Program.ControlScheme.Swipe && !idListSwipe.Contains(f.sessionID))
                {
                    idListSwipe.Add(f.sessionID);
                }
                if (f.fileTag == Program.ControlScheme.Tap && !idListTap.Contains(f.sessionID))
                {
                    idListTap.Add(f.sessionID);
                }

                //this last one is for if ALL lists contain this session ID
                if (idListTap.Contains(f.sessionID) && idListSwipe.Contains(f.sessionID) && idListTilt.Contains(f.sessionID) && !idListAll.Contains(f.sessionID))
                {
                    idListAll.Add(f.sessionID);
                }
            }

            //output the size of these lists
            WriteToConsole("Total Sessions Per Mode: " + idList.Count() + " Swipe: " + idListSwipe.Count() + " Tap: " + idListTilt.Count() + " Tilt: " + idListTap.Count() + " All three: " + idListAll.Count());
        }

        /// <summary>
        /// Count the number of times the player character dies
        /// </summary>
        public void CountDeaths()
        {
            int i = 0;
            int iTap = 0;
            int iTilt = 0;
            int iSwipe = 0;

            //for each event in each round in each file.
            foreach (FileOfRounds f in _ListOfFiles)
            {
                foreach (RoundOfEvents r in f._roundsInFile)
                {
                    foreach (Event e in r._eventsInRound)
                    {

                        //if this event is a death, increment the count
                        if (e._tag == Program.EventTag.Died)
                        {
                            i++;
                            switch (r.roundTag)
                            {
                                case Program.ControlScheme.Swipe:
                                    iSwipe++;
                                    break;
                                case Program.ControlScheme.Tap:
                                    iTap++;
                                    break;
                                case Program.ControlScheme.Tilt:
                                    iTilt++;
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
            }
            WriteToConsole("Total Died: " + i + " Swipe: " + iSwipe + " Tap: " + iTap + " Tilt: " + iTilt);
        }


        /// <summary>
        /// Count events with the given tag
        /// </summary>
        /// <param name="t"></param>
        public void CountEvent(Program.EventTag t)
        {
            int i = 0;
            int iTap = 0;
            int iTilt = 0;
            int iSwipe = 0;
            foreach (FileOfRounds f in _ListOfFiles)
            {
                foreach (RoundOfEvents r in f._roundsInFile)
                {
                    foreach (Event e in r._eventsInRound)
                    {

                        if (e._tag == t)
                        {
                            i++;
                            switch (r.roundTag)
                            {
                                case Program.ControlScheme.Swipe:
                                    iSwipe++;
                                    break;
                                case Program.ControlScheme.Tap:
                                    iTap++;
                                    break;
                                case Program.ControlScheme.Tilt:
                                    iTilt++;
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
            }
            WriteToConsole("Total " + t.ToString() + ": " + i + " Swipe: " + iSwipe + " Tap: " + iTap + " Tilt: " + iTilt);
        }

        /// <summary>
        /// Count all events.
        /// </summary>
        public void CountAllEvents()
        {
            int i = 0;
            int iTap = 0;
            int iTilt = 0;
            int iSwipe = 0;
            foreach (FileOfRounds f in _ListOfFiles)
            {
                foreach (RoundOfEvents r in f._roundsInFile)
                {
                    foreach (Event e in r._eventsInRound)
                    {
                        i++;
                        switch (r.roundTag)
                        {
                            case Program.ControlScheme.Swipe:
                                iSwipe++;
                                break;
                            case Program.ControlScheme.Tap:
                                iTap++;
                                break;
                            case Program.ControlScheme.Tilt:
                                iTilt++;
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            WriteToConsole("Total: " + i + " Swipe: " + iSwipe + " Tap: " + iTap + " Tilt: " + iTilt);
        }

        /// <summary>
        /// Count events where the player takes damage from spikes
        /// </summary>
        public void CountSpikeDamage()
        {
            int i = 0;
            int iTap = 0;
            int iTilt = 0;
            int iSwipe = 0;
            foreach (FileOfRounds f in _ListOfFiles)
            {
                foreach (RoundOfEvents r in f._roundsInFile)
                {
                    foreach (Event e in r._eventsInRound)
                    {

                        if (e._tag == Program.EventTag.Damaged && e._damageSource == Program.DamageSource.Spikes)
                        {
                            i++;
                            switch (r.roundTag)
                            {
                                case Program.ControlScheme.Swipe:
                                    iSwipe++;
                                    break;
                                case Program.ControlScheme.Tap:
                                    iTap++;
                                    break;
                                case Program.ControlScheme.Tilt:
                                    iTilt++;
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
            }
            WriteToConsole("Total: " + i + " Swipe: " + iSwipe + " Tap: " + iTap + " Tilt: " + iTilt);
        }

        /// <summary>
        /// Count events where the same event happens twice within timediff seconds
        /// </summary>
        /// <param name="t"></param>
        /// <param name="timeDiff"></param>
        public void CountEventRetry(Program.EventTag t, float timeDiff)
        {
            float lastTime = 0; //the last time this event happened
            int i = 0;
            int iTap = 0;
            int iTilt = 0;
            int iSwipe = 0;
            foreach (FileOfRounds f in _ListOfFiles)
            {
                foreach (RoundOfEvents r in f._roundsInFile)
                {
                    foreach (Event e in r._eventsInRound)
                    {

                        if (e._tag == t)
                        {
                            //if it's sooner than the previous time plus the time difference, count it
                            if ((lastTime + timeDiff) >= e._time)
                            {
                                i++;
                                switch (r.roundTag)
                                {
                                    case Program.ControlScheme.Swipe:
                                        iSwipe++;
                                        break;
                                    case Program.ControlScheme.Tap:
                                        iTap++;
                                        break;
                                    case Program.ControlScheme.Tilt:
                                        iTilt++;
                                        break;
                                    default:
                                        break;
                                }
                            }

                            //set the current time to the last time the event happened.
                            lastTime = e._time;
                        }
                    }
                }
            }

            WriteToConsole("Total " + t.ToString() + "within "+timeDiff+": " + i + " Swipe: " + iSwipe + " Tap: " + iTap + " Tilt: " + iTilt);
        }

        /// <summary>
        /// Count the number of times the player tried to change direction and failed
        /// </summary>
        public void CountFailedTryChange()
        {
            int i = 0;
            int iTap = 0;
            int iTilt = 0;
            int iSwipe = 0;
            foreach (FileOfRounds f in _ListOfFiles)
            {
                foreach (RoundOfEvents r in f._roundsInFile)
                {
                    foreach (Event e in r._eventsInRound)
                    {

                        if (e._tag == Program.EventTag.TryChange && e._succeedChanging == false)
                        {
                            i++;
                            switch (r.roundTag)
                            {
                                case Program.ControlScheme.Swipe:
                                    iSwipe++;
                                    break;
                                case Program.ControlScheme.Tap:
                                    iTap++;
                                    break;
                                case Program.ControlScheme.Tilt:
                                    iTilt++;
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
            }
            WriteToConsole("Total Failed Attempts To Change Direction: " + i + " Swipe: " + iSwipe + " Tap: " + iTap + " Tilt: " + iTilt);
        }

        /// <summary>
        /// Get the average health remaining from all the times the player survived the round
        /// </summary>
        public void HealthIfSurvived()
        {
            float i = 0, iRound = 0;
            float iTap = 0, iRoundTap = 0;
            float iTilt = 0, iRoundTilt = 0;
            float iSwipe = 0, iRoundSwipe = 0;
            foreach (FileOfRounds f in _ListOfFiles)
            {
                foreach (RoundOfEvents r in f._roundsInFile)
                {
                    foreach (Event e in r._eventsInRound)
                    {

                        if (e._tag == Program.EventTag.TimeEnd) //if time ends without dying, add the health left
                        {
                            i = i + e._healthLeft;
                            iRound++;
                            switch (r.roundTag)
                            {
                                case Program.ControlScheme.Swipe:
                                    iSwipe = iSwipe + e._healthLeft;
                                    iRoundSwipe++;
                                    break;
                                case Program.ControlScheme.Tap:
                                    iTap = iTap + e._healthLeft;
                                    iRoundTap++;
                                    break;
                                case Program.ControlScheme.Tilt:
                                    iTilt = iTilt + e._healthLeft;
                                    iRoundTilt++;
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
            }
            //divide total by count to get the averages
            WriteToConsole("Average Health Remaining: " + i / iRound + " Swipe: " + iSwipe / iRoundSwipe + " Tap: " + iTap / iRoundTap + " Tilt: " + iTilt / iRoundTilt);
        }

        /// <summary>
        /// Get the average final score from each round
        /// </summary>
        public void ScoreAverage()
        {
            float i = 0, iRound = 0;
            float iTap = 0, iRoundTap = 0;
            float iTilt = 0, iRoundTilt = 0;
            float iSwipe = 0, iRoundSwipe = 0;
            foreach (FileOfRounds f in _ListOfFiles)
            {
                foreach (RoundOfEvents r in f._roundsInFile)
                {
                    foreach (Event e in r._eventsInRound)
                    {

                        if (e._tag == Program.EventTag.Died || e._tag == Program.EventTag.TimeEnd)
                        {
                            i = i + e._score;
                            iRound++;
                            switch (r.roundTag)
                            {
                                case Program.ControlScheme.Swipe:
                                    iSwipe = iSwipe + e._score;
                                    iRoundSwipe++;
                                    break;
                                case Program.ControlScheme.Tap:
                                    iTap = iTap + e._score;
                                    iRoundTap++;
                                    break;
                                case Program.ControlScheme.Tilt:
                                    iTilt = iTilt + e._score;
                                    iRoundTilt++;
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
            }
            WriteToConsole("Score Average Overall: " + i / iRound + " Swipe: " + iSwipe / iRoundSwipe + " Tap: " + iTap / iRoundTap + " Tilt: " + iTilt / iRoundTilt);
        }

        /// <summary>
        /// Return the average score from a single file of rounds
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public float ScoreAverageSingleFile(FileOfRounds f)
        {
            float i = 0, iRound = 0;

            foreach (RoundOfEvents r in f._roundsInFile)
            {
                foreach (Event e in r._eventsInRound)
                {

                    if (e._tag == Program.EventTag.Died || e._tag == Program.EventTag.TimeEnd)
                    {
                        i = i + e._score;
                        iRound++;
                    }
                }
            }

            return i / iRound;
        }

        /// <summary>
        /// Get the score from a single round
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        public float ScoreSingleRound(RoundOfEvents r)
        {

            foreach (Event e in r._eventsInRound)
            {

                if (e._tag == Program.EventTag.Died || e._tag == Program.EventTag.TimeEnd)
                {
                    return e._score;
                }
            }

            return 0;
        }

        /// <summary>
        /// Get whether or not the player died in a single round
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        public bool DiedSingleRound(RoundOfEvents r)
        {

            foreach (Event e in r._eventsInRound)
            {

                if (e._tag == Program.EventTag.Died)
                {
                    return true;
                }
            }

            return false;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////
        //////// PRINTING CSV ///////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////

        // these next few output comma separated lists that can be copied directly into Excel or similar

        /// <summary>
        /// For each round, print the session ID, the score, and the control rating
        /// </summary>
        public void ForRoundPrintScoreAndControl()
        {
            float i = 0;
            foreach (FileOfRounds f in _ListOfFiles)
            {
                foreach (RoundOfEvents r in f._roundsInFile)
                {
                    foreach (Event e in r._eventsInRound)
                    {

                        if (e._tag == Program.EventTag.Died || e._tag == Program.EventTag.TimeEnd)
                        {
                            i = e._score;
                        }

                    }
                    WriteToConsole(r.sessionID + "," + r.roundTag + "," + i + "," + f._control);
                }
            }
        }

        /// <summary>
        /// Print the round tag and the damage taken in total in that round
        /// </summary>
        public void PrintRoundDamage()
        {

            foreach (FileOfRounds f in _ListOfFiles)
            {
                foreach (RoundOfEvents r in f._roundsInFile)
                {
                    float i = 0;
                    foreach (Event e in r._eventsInRound)
                    {

                        if (e._tag == Program.EventTag.Damaged)
                        {
                            i++;

                        }

                    }
                    WriteToConsole(r.roundTag + "," + i);
                }
            }
        }

        /// <summary>
        /// Print the round tag and the length of time lived in that round
        /// </summary>
        public void PrintRoundLivedTime()
        {

            foreach (FileOfRounds f in _ListOfFiles)
            {
                foreach (RoundOfEvents r in f._roundsInFile)
                {
                    float roundStart = 0;
                    bool firstEvent = true;
                    foreach (Event e in r._eventsInRound)
                    {
                        if (firstEvent)
                        {
                            firstEvent = false;
                            roundStart = e._time;
                        }
                        if (e._tag == Program.EventTag.Died)
                        {

                            WriteToConsole(r.roundTag + "," + (e._time - roundStart));
                        }

                    }

                }
            }
        }

        /// <summary>
        /// For each round, print the average distance between a Tap start and end, plus the standard deviation
        /// </summary>
        public void PrintTapDistance()
        {

            foreach (FileOfRounds f in _ListOfFiles)
            {
                if (f.fileTag == Program.ControlScheme.Tap)
                {
                    foreach (RoundOfEvents r in f._roundsInFile)
                    {
                        int i = 0;
                        float totalDistance = 0;
                        List<float> listForSD = new List<float>();

                        foreach (Event e in r._eventsInRound)
                        {
                            if (e._tag == Program.EventTag.Tap)
                            {
                                //get the current location as numbers
                                Char[] delimiter = new Char[] { ',', '(', ')' };
                                String[] substrings = e._currentLocation.Split(delimiter);
                                float x = float.Parse(substrings[1]);
                                float y = float.Parse(substrings[3]);

                                //get the goal location as numbers
                                substrings = e._tapLocation.Split(delimiter);
                                float x2 = float.Parse(substrings[1]);
                                float y2 = float.Parse(substrings[3]);

                                //get the distance
                                float distance = (x - x2) + (y - y2);
                                distance = (float)Math.Sqrt(distance * distance);

                                //add the data
                                i++;
                                totalDistance = totalDistance + distance;
                                listForSD.Add(distance);
                            }
                        }

                        //calculate the standard deviation (square root of average distance from mean)
                        float mean = totalDistance / i;
                        float stanDev = 0;

                        foreach (float fl in listForSD)
                        {
                            stanDev = stanDev + ((fl - mean) * (fl - mean));
                        }

                        stanDev = (float)Math.Sqrt(stanDev / i);

                        WriteToConsole(f.sessionID + ": " + mean.ToString() + "," + stanDev.ToString());
                    }

                }
            }
        }

        /// <summary>
        /// This orders the files by the time they were created and then prints a list of file tags
        /// </summary>
        public void OrderFilesByTimeAndPrint()
        {
            List<FileOfRounds> _currentList = new List<FileOfRounds>();
            _currentList.Add(null);
            string _currentID = null;

            //for each file
            foreach (FileOfRounds f in _ListOfFiles)
            {
                //keep a list of files with the same sessionID
                if (f.sessionID == _currentID)
                {
                    _currentList.Add(f);
                }
                //when the next file will have a new sessionID
                else
                {
                    //add a blank end to the file
                    _currentList.Add(null);

                    //order the files, then print a CSV line of them in order
                    OrderSublistByTimeAndPrint(_currentList);

                    //clear the list and add the next file
                    _currentList.Clear();
                    _currentList.Add(f);
                    _currentID = f.sessionID;
                }
            }
            _currentList.Add(null);
            //order the final list of files, then print a CSV line of them in order
            OrderSublistByTimeAndPrint(_currentList);
        }

        /// <summary>
        /// order a sublist of files by time order, and print a csv list of file tags
        /// </summary>
        /// <param name="_fileList"></param>
        void OrderSublistByTimeAndPrint(List<FileOfRounds> _fileList)
        {
            int i = 0;

            //order the files the normal way
            for (i = 0; _fileList[i] != null; i++)
            {
                int j = i;
                while (j > 0)
                {
                    if (_fileList[j - 1]._uploadTime.CompareTo(_fileList[j]._uploadTime) > 0)
                    {
                        FileOfRounds temp = _fileList[j - 1];
                        _fileList[j - 1] = _fileList[j];
                        _fileList[j] = temp;
                        j--;
                    }
                    else
                        break;
                }

            }

            //this next bit prints one line for each sublist, like "Tap, Tap, Swipe,"
            foreach (FileOfRounds f in _fileList)
            {
                if (f != null)
                    //adjust this bit to change what gets printed.
                    Console.Write(f.fileTag + ",");
            }
            Console.Write("\n");
        }

        /// <summary>
        /// similar to OrderFilesByTimeAndPrint but prints a list of file scores
        /// </summary>
        public void OrderFilesByTimeAndPrintScores()
        {
            List<FileOfRounds> _currentList = new List<FileOfRounds>();
            _currentList.Add(null);
            string _currentID = null;
            //for each file
            foreach (FileOfRounds f in _ListOfFiles)
            {
                //keep a list of files with the same sessionID
                if (f.sessionID == _currentID)
                {
                    _currentList.Add(f);
                }
                //when the next file will have a new sessionID
                else
                {
                    _currentList.Add(null);
                    //order the files, then print a CSV line of them in order
                    OrderSublistByTimeAndPrintRoundScores(_currentList);

                    //clear the list and add the next file
                    _currentList.Clear();
                    _currentList.Add(f);
                    _currentID = f.sessionID;
                }
            }
            _currentList.Add(null);
            //order the files, then print a CSV line of them in order
            OrderSublistByTimeAndPrintRoundScores(_currentList);
        }

        /// <summary>
        /// similar to OrderSublistByTimeAndPrint but prints the Control scores for each round
        /// </summary>
        /// <param name="_fileList"></param>
        void OrderSublistByTimeAndPrintRoundScores(List<FileOfRounds> _fileList)
        {
            int i = 0;
            for (i = 0; _fileList[i] != null; i++)
            {
                int j = i;
                while (j > 0)
                {
                    if (_fileList[j - 1]._uploadTime.CompareTo(_fileList[j]._uploadTime) > 0)
                    {
                        FileOfRounds temp = _fileList[j - 1];
                        _fileList[j - 1] = _fileList[j];
                        _fileList[j] = temp;
                        j--;
                    }
                    else
                        break;
                }

            }
            foreach (FileOfRounds f in _fileList)
            {
                if (f != null)
                {
                    Console.Write(f.fileTag + "," + f._control + ",");
                }
            }
            Console.Write("\n");
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////////
        //////// HEATMAPS ///////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////////

        //Heatmaps are SVG format files. You can use the blank map background to overlay them on - note that
        //they automatically print upside down because of SVG and Unity having different zeroes.


        /// <summary>
        /// Build a heatmap of the given event for the given control scheme
        /// </summary>
        /// <param name="eventTag"></param>
        /// <param name="controlScheme"></param>
        public void BuildHeatmapEvent(Program.EventTag eventTag, Program.ControlScheme controlScheme)
        {
            List<Vector2> locationList = new List<Vector2>();

            //get a list of locations
            foreach (FileOfRounds f in _ListOfFiles)
            {
                if (f.fileTag == controlScheme || controlScheme == Program.ControlScheme.All)
                {
                    foreach (RoundOfEvents r in f._roundsInFile)
                    {
                        foreach (Event e in r._eventsInRound)
                        {

                            if (e._tag == eventTag)
                            {
                                //cut the event location into a Vector2 and add it to the list
                                Char[] delimiter = new Char[] { ',', '(', ')' };
                                String[] substrings = e._currentLocation.Split(delimiter);
                                float x = float.Parse(substrings[1]);
                                float y = float.Parse(substrings[3]);
                                locationList.Add(new Vector2(x, y));
                            }
                        }
                    }
                }
            }

            //open an SVG file
            using (System.IO.StreamWriter file = new System.IO.StreamWriter("latestOutput.svg"))
            {
                file.WriteLine("<svg width = \"400\" height = \"400\">\n");
                WriteToConsole("start");

                //write a single SVG circle for each event
                foreach (Vector2 v in locationList)
                {
                    file.WriteLine(SingleSVGCircle(v.x * 10, v.y * 10));
                }

                //close the file
                file.WriteLine("</svg>\n");
                WriteToConsole("end");
            }
        }

        /// <summary>
        /// Return a line of SVG for a single semitransparent red circle at the given location. You might want to
        /// adjust opacity depending on how many points overlap
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        string SingleSVGCircle(float x, float y)
        {
            string s = "<circle cx = \"" + x + "\" cy = \"" + y + "\" r = \"3\" style=\"fill: red; stroke: none; stroke - width:0; opacity: 0.1\"/>\n";
            return s;
        }

        /// <summary>
        /// Return a line of SVG for a single line from one vector to another
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        string SingleSVGLine(Vector2 a, Vector2 b)
        {
            string s = "<circle cx = \"" + a.x * 10 + "\" cy = \"" + a.y * 10 + "\" r = \"3\" style=\"fill: red; stroke: none; stroke - width:0; opacity: 0.1\"/>\n"
            + "<line x1=\"" + a.x * 10 + "\" y1=\"" + a.y * 10 + "\" x2=\"" + b.x * 10 + "\" y2=\"" + b.y * 10 + "\" style=\"stroke: red; stroke - width:4\" />";
            return s;
        }

        /// <summary>
        /// Return a line of SVG for a circle at the given co-ords with a short line pointing in Newdir direction from it
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="newdir"></param>
        /// <returns></returns>
        string SingleSVGCircleWithDirections(float x, float y, Vector2 newdir)
        {
            float endX = x + (newdir.x * 10);
            float endY = y + (newdir.y * 10);
            string s = "<circle cx = \"" + x + "\" cy = \"" + y + "\" r = \"3\" style=\"fill: red; stroke: none; stroke - width:0; opacity: 0.1\"/>\n" +
                "<line x1=\"" + x + "\" y1=\"" + y + "\" x2=\"" + endX + "\" y2=\"" + endY + "\" style=\"stroke: red; stroke - width:4\" />";

            return s;
        }

        /// <summary>
        /// Build a heatmap for TryChange events in the given control scheme that succeed or fail depending on the bool
        /// Each event will have a short line marking it's new direction
        /// </summary>
        /// <param name="controlScheme"></param>
        /// <param name="success"></param>
        public void BuildHeatmapTryChangeWithLines(Program.ControlScheme controlScheme, bool success)
        {
            List<Vector2> locationList = new List<Vector2>();
            List<Vector2> vectorOutList = new List<Vector2>();

            //get a list of locations
            foreach (FileOfRounds f in _ListOfFiles)
            {
                if (f.fileTag == controlScheme || controlScheme == Program.ControlScheme.All)
                {
                    foreach (RoundOfEvents r in f._roundsInFile)
                    {
                        foreach (Event e in r._eventsInRound)
                        {

                            if (e._tag == Program.EventTag.TryChange && e._succeedChanging == success)
                            {

                                Char[] delimiter = new Char[] { ',', '(', ')' };
                                String[] substrings = e._currentLocation.Split(delimiter);
                                float x = float.Parse(substrings[1]);
                                float y = float.Parse(substrings[3]);
                                locationList.Add(new Vector2(x, y));

                                substrings = e._newDirection.Split(delimiter);
                                float x2 = float.Parse(substrings[1]);
                                float y2 = float.Parse(substrings[3]);
                                vectorOutList.Add(new Vector2(x2, y2));
                            }
                        }
                    }
                }
            }

            //open an SVG file
            using (System.IO.StreamWriter file = new System.IO.StreamWriter("latestOutput.svg"))
            {
                file.WriteLine("<svg width = \"400\" height = \"400\">\n");
                WriteToConsole("start");

                for (int i = 0; i < locationList.Count; i++)
                {
                    Vector2 v = locationList[i];
                    file.WriteLine(SingleSVGCircleWithDirections(v.x * 10, v.y * 10, vectorOutList[i]));
                    WriteToConsole(SingleSVGCircleWithDirections(v.x * 10, v.y * 10, vectorOutList[i]));
                }

                //close the file
                file.WriteLine("</svg>\n");
                WriteToConsole("end");
            }
            //write each location to it as a small semitransparent circle


        }

        /// <summary>
        /// Build heatmap of death locations
        /// </summary>
        /// <param name="controlScheme"></param>
        public void BuildHeatmapDied(Program.ControlScheme controlScheme)
        {
            List<Vector2> locationList = new List<Vector2>();

            //get a list of locations
            foreach (FileOfRounds f in _ListOfFiles)
            {
                if (f.fileTag == controlScheme || controlScheme == Program.ControlScheme.All)
                {
                    foreach (RoundOfEvents r in f._roundsInFile)
                    {
                        foreach (Event e in r._eventsInRound)
                        {

                            if (e._tag == Program.EventTag.Damaged && e._newHealth == 0)
                            {

                                Char[] delimiter = new Char[] { ',', '(', ')' };
                                String[] substrings = e._currentLocation.Split(delimiter);
                                float x = float.Parse(substrings[1]);
                                float y = float.Parse(substrings[3]);
                                locationList.Add(new Vector2(x, y));
                            }
                        }
                    }
                }
            }

            //open an SVG file
            using (System.IO.StreamWriter file = new System.IO.StreamWriter("latestOutput.svg"))
            {
                file.WriteLine("<svg width = \"400\" height = \"400\">\n");
                WriteToConsole("start");

                foreach (Vector2 v in locationList)
                {
                    file.WriteLine(SingleSVGCircle(v.x * 10, v.y * 10));
                    WriteToConsole(SingleSVGCircle(v.x, v.y));
                }

                //close the file
                file.WriteLine("</svg>\n");
                WriteToConsole("end");
            }
            //write each location to it as a small semitransparent circle


        }

        /// <summary>
        /// Build a map of tap locations and lines for a given round number
        /// </summary>
        /// <param name="r1"></param>
        /// <param name="s"></param>
        public void BuildLineMapSpecificRounds(int r1, Program.ControlScheme s)
        {
            List<Vector2> locationList = new List<Vector2>();
            List<Vector2> locationEndList = new List<Vector2>();
            int i = 0;

            //get a list of locations
            foreach (FileOfRounds f in _ListOfFiles)
            {
                if (s == f.fileTag)
                {

                    foreach (RoundOfEvents r in f._roundsInFile)
                    {
                        i++;
                        if (i == r1)
                        {
                            foreach (Event e in r._eventsInRound)
                            {

                                if (e._tag == Program.EventTag.Tap)
                                {

                                    Char[] delimiter = new Char[] { ',', '(', ')' };
                                    String[] substrings = e._currentLocation.Split(delimiter);
                                    float x = float.Parse(substrings[1]);
                                    float y = float.Parse(substrings[3]);
                                    locationList.Add(new Vector2(x, y));

                                    substrings = e._tapLocation.Split(delimiter);
                                    float x2 = float.Parse(substrings[1]);
                                    float y2 = float.Parse(substrings[3]);
                                    locationEndList.Add(new Vector2(x2, y2));

                                    float distance = (x - x2) + (y - y2);

                                    distance = (float)Math.Sqrt(distance * distance);
                                    WriteToConsole(distance.ToString());
                                }
                            }
                        }
                    }
                }
            }

            //open an SVG file
            using (System.IO.StreamWriter file = new System.IO.StreamWriter("latestOutput.svg"))
            {
                file.WriteLine("<svg width = \"400\" height = \"400\">\n");
                WriteToConsole("start");

                for (int j = 0; j < locationList.Count(); j++)
                {
                    file.WriteLine(SingleSVGLine(locationList[j], locationEndList[j]));
                }

                file.Write("<polyline points=\"");
                foreach (Vector2 v in locationList)
                {
                    file.Write(v.x * 10 + "," + v.y * 10 + " ");
                }
                file.Write("\" style=\"fill: none; stroke: black; stroke - width:3\" />/n");

                //close the file
                file.WriteLine("</svg>\n");
                WriteToConsole("end");
            }
            //write each location to it as a small semitransparent circle
        }

    }
}
