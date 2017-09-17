using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;

namespace TactusDataAnalytics
{

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    //////// PROGRAM ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public class Program
    {
        // The Event Tag enumerator identifies what type of event an event is.
        public enum EventTag { None, Start, Tap, TryChange, Damaged, Died, TimeEnd, Powerup, CaughtEnemy, End, Scores }
        
        //This identifies what Control Scheme a round or file is using
        public enum ControlScheme { Tap, Swipe, Tilt, All }

        //when the player takes damage, this tracks the source.
        public enum DamageSource { None, Enemy, Spikes }

        //This is the main function, it automatically runs when the program starts and creates an Application which does most of the work
        static void Main(string[] args)
        {

            Application a = new Application();
            
            //read in the file list in the given folder and store the data
            a.ReadFileList("files");

            //Replace this with whatever function you want to run.
            a.CountAllEvents();

            //this stops the program closing for long enough for you to read the results
            for (;;) { }
        }
    }

    
    /// <summary>
    /// A simple class for a 2D vector
    /// </summary>
    public class Vector2
    {
        public float x;
        public float y;

        public Vector2(float newX, float newY)
        {
            this.x = newX;
            this.y = newY;
        }
    }

    /// <summary>
    /// This class stores a File Of Rounds, which includes a list of rounds
    /// and all the data that needs to be stored on a file basis.
    /// </summary>
    public class FileOfRounds
    {
        //list of rounds
        public List<RoundOfEvents> _roundsInFile;
        public Program.ControlScheme fileTag;
        public string sessionID;

        public int _control;
        public int _challenge;
        public int _fun;

        public DateTime _uploadTime;
    }

    /// <summary>
    /// A round's worth of events,
    /// stores a list of Event files and the tag and ID for the round
    /// </summary>
    public class RoundOfEvents
    {
        //list of events

        public List<Event> _eventsInRound;
        public Program.ControlScheme roundTag;
        public string sessionID;
    }

    /// /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// ///////// EVENTS ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    ///This event class stores all the data for all the different types of event including default values
    ///if you add more events, put the data in here - or redo it
    public class Event
    {
        public Program.EventTag _tag; //the tag for the kind of event
        public float _time; //the time at which it happened as a float
        public string _currentLocation; //the player location at the time

        //tap location for TAP events
        public string _tapLocation;

        //Try Change Direction events
        public string _currentDirection; //the current direction
        public string _newDirection; //the direction trying to change do
        public bool _succeedChanging; //was it successful

        //Damaged event
        public int _newHealth = -1; //what's the player's health now
        public Program.DamageSource _damageSource; //what caused the damage?

        //End event/died event
        public int _score = -1; //what was the final score
        public int _healthLeft = -1; //how much health remains

        //died event


        //default event constructors
        public Event()
        {
        }
        
    }

    /// <summary>
    /// This is a special Event that holds the response scores from the player.
    /// </summary>
    public class EventResponseScore
    {

        public int _control;
        public int _challenge;
        public int _fun;

        public EventResponseScore(int control, int challenge, int fun)
        {
            this._control = control;
            this._challenge = challenge;
            this._fun = fun;
        }

    }
}
