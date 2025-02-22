/*
Copyright (C) 2015 Rory Walsh. 
Android support and asset management changes by Hector Centeno

This interface would not have been possible without Richard Henninger's .NET interface to the Csound API. 
 
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), 
to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR 
ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH 
THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System;
using System.Globalization;
#if UNITY_EDITOR
using UnityEditor;
//using System.Threading.Tasks;
#endif
#if UNITY_ANDROID
using UnityEngine.Networking;
#endif
#if UNITY_EDITOR || UNITY_STANDALONE
using MYFLT = System.Double;
#elif UNITY_ANDROID // and maybe iOS?
using MYFLT = System.Single;
#endif

#region PUBLIC_CLASSES

///// <summary>
///// Utility class for CsoundFiles to copy 
///// </summary>
//[Serializable]
//public struct CsoundFilesInfo
//{
//    public string[] fileNames;
//}

#endregion PUBLIC_CLASSES

/// <summary>
/// Csound Unity Class
/// </summary>
[AddComponentMenu("Audio/CsoundUnity")]
[Serializable]
[RequireComponent(typeof(AudioSource))]
public class CsoundUnityPure : MonoBehaviour
{
    #region PUBLIC_FIELDS

    /// <summary>
    /// The name of this package
    /// </summary>
    public const string packageName = "com.csound.csoundunity";

    /// <summary>
    /// The version of this package
    /// </summary>
    public const string packageVersion = "3.0.0";

    /// <summary>
    /// the unique guid of the csd file
    /// </summary>
    public string csoundFileGUID { get => _csoundFileGUID; }

    /// <summary>
    /// The file CsoundUnity will try to load. You can only load one file with each instance of CsoundUnity,
    /// but you can use as many instruments within that file as you wish.You may also create as many
    /// of CsoundUnity objects as you wish. 
    /// </summary>
    public string csoundFileName { get => _csoundFileName; }

    /// <summary>
    /// a string to hold all the csoundFile content
    /// </summary>
    public string csoundString { get => _csoundString; }

#if UNITY_EDITOR
    /// <summary>
    /// a reference to a csd file as DefaultAsset 
    /// </summary>
    //[SerializeField]
    public DefaultAsset csoundAsset { get => _csoundAsset; }
#endif

    /// <summary>
    /// If true, all Csound output messages will be sent to the 
    /// Unity output console.
    /// Note that this can slow down performance if there is a
    /// lot of information being printed.
    /// </summary>
    public bool logCsoundOutput = false;

    /// <summary>
    /// If true no audio is sent to output
    /// </summary>
    public bool mute = false;

    /// <summary>
    /// If true Csound uses as an input the AudioClip attached to this AudioSource
    /// If false, no processing occurs on the attached AudioClip
    /// </summary>
    public bool processClipAudio;

    /// <summary>
    /// If true it will print warnings in the console when the output volume is too high, 
    /// and mute all the samples above the loudWarningThreshold value
    /// </summary>
    public bool loudVolumeWarning = true;

    /// <summary>
    /// The volume threshold at which a warning is output to the console, 
    /// and audio output is filtered
    /// </summary>
    public float loudWarningThreshold = 10f;

    /// <summary>
    /// list to hold channel data
    /// </summary>
    public List<CsoundChannelController> channels { get => _channels; }

    /// <summary>
    /// list to hold available audioChannels names
    /// </summary>
    public List<string> availableAudioChannels { get => _availableAudioChannels; }

    /// <summary>
    /// public named audio Channels shown in CsoundUnityChild inspector
    /// </summary>
    public readonly Dictionary<string, MYFLT[]> namedAudioChannelDataDict = new Dictionary<string, MYFLT[]>();

    /// <summary>
    /// Is Csound initialized?
    /// </summary>
    public bool IsInitialized { get => initialized; }

    /// <summary>
    /// The delegate of the event OnCsoundInitialized
    /// </summary>
    public delegate void CsoundInitialized();
    /// <summary>
    /// An event that will be executed when Csound is initialized
    /// </summary>
    public event CsoundInitialized OnCsoundInitialized;

    public bool PerformanceFinished { get => performanceFinished; }
    /// <summary>
    /// the score to send via editor
    /// </summary>
    public string csoundScore;

    #endregion PUBLIC_FIELDS

    #region PRIVATE_FIELDS

    /// <summary>
    /// The private member variable csound provides access to the CsoundUnityBridge class, which 
    /// is defined in the CsoundUnity native libraries. If for some reason the libraries can 
    /// not be found, Unity will report the issue in its output Console. The CsoundUnityBridge object
    /// provides access to Csounds low level native functions. The csound object is defined as private,
    /// meaning other scripts cannot access it. If other scripts need to call any of Csounds native
    /// fuctions, then methods should be added to the CsoundUnity.cs file and CsoundUnityBridge class.
    /// </summary>
    string dataPath;
    private CsoundUnityBridge csound;
    [SerializeField] private string _csoundFileGUID;
    [SerializeField] private string _csoundString;
    [SerializeField] private string _csoundFileName;
#if UNITY_EDITOR
    [SerializeField] private DefaultAsset _csoundAsset;
#endif
    [SerializeField] private List<CsoundChannelController> _channels = new List<CsoundChannelController>();
    /// <summary>
    /// An utility dictionary to store the index of every channel in the _channels list
    /// </summary>
    private Dictionary<string, int> _channelsIndexDict = new Dictionary<string, int>();
    [SerializeField] private List<string> _availableAudioChannels = new List<string>();
    /// <summary>
    /// Inspector foldout settings
    /// </summary>
#pragma warning disable 414
    [SerializeField] private bool _drawCsoundString = false;
    [SerializeField] private bool _drawTestScore = false;
    [SerializeField] private bool _drawSettings = false;
    [SerializeField] private bool _drawChannels = false;
    [SerializeField] private bool _drawAudioChannels = false;
#pragma warning restore 414

    private bool initialized = false;
    private uint ksmps = 32;
    private uint ksmpsIndex = 0;
    private float zerdbfs = 1;
    private bool compiledOk = false;
    private bool performanceFinished;
    private AudioSource audioSource;
    private Coroutine LoggingCoroutine;
    int bufferSize, numBuffers;

    /// <summary>
    /// the temp buffer, ksmps sized 
    /// </summary>
    private Dictionary<string, MYFLT[]> namedAudioChannelTempBufferDict = new Dictionary<string, MYFLT[]>();

    #endregion

    public void ReDoCsound(string _csoundString)
    {
        /// the CsoundUnityBridge constructor takes a path to the package Runtime folder, and a string with the csound code.
        /// It then calls createCsound() to create an instance of Csound and compile the csd string.
        /// After this we start the performance of Csound.
        csound = new CsoundUnityBridge(dataPath, _csoundString);
        if (csound != null)
        {
            /// channels are created when a csd file is selected in the inspector
            if (channels != null)
                // initialise channels if found in xml descriptor..
                for (int i = 0; i < channels.Count; i++)
                {
                    if (channels[i].type.Contains("combobox"))
                        csound.SetChannel(channels[i].channel, channels[i].value + 1);
                    else
                        csound.SetChannel(channels[i].channel, channels[i].value);
                    // update channels index dictionary
                    if (!_channelsIndexDict.ContainsKey(channels[i].channel))
                        _channelsIndexDict.Add(channels[i].channel, i);
                }

            foreach (var name in availableAudioChannels)
            {
                if (!namedAudioChannelDataDict.ContainsKey(name))
                {
                    namedAudioChannelDataDict.Add(name, new MYFLT[bufferSize]);
                    namedAudioChannelTempBufferDict.Add(name, new MYFLT[ksmps]);
                }
            }

            // /// This coroutine prints the Csound output to the Unity console
            // LoggingCoroutine = StartCoroutine(Logging(.01f));

            compiledOk = csound.CompiledWithoutError();

            if (compiledOk)
            {
                zerdbfs = (float)csound.Get0dbfs();

                Debug.Log("zerdbfs " + zerdbfs);

                //#if UNITY_EDITOR || UNITY_STANDALONE
                //                csound.SetStringChannel("CsoundFiles", Application.streamingAssetsPath + "/CsoundFiles/");
                //                csound.SetStringChannel("AudioPath", Application.dataPath + "/Audio/");
                //                if (Application.isEditor)
                //                    csound.SetStringChannel("CsoundFiles", Application.streamingAssetsPath + "/CsoundFiles/");
                //                else
                //                    csound.SetStringChannel("CsoundFiles", Application.streamingAssetsPath + "/CsoundFiles/");
                //                csound.SetStringChannel("StreamingAssets", Application.streamingAssetsPath);
                //#elif UNITY_ANDROID
                //                string persistentPath = Application.persistentDataPath + "/CsoundFiles/"; // TODO ??
                //                csound.SetStringChannel("CsoundFilesPath", Application.dataPath);
                //#endif
                initialized = true;
                OnCsoundInitialized?.Invoke();
            }
        }
        else
        {
            Debug.Log("Error creating Csound object");
            compiledOk = false;
        }
    }

    /// <summary>
    /// CsoundUnity Awake function. Called when this script is first instantiated. This should never be called directly. 
    /// This functions behaves in more or less the same way as a class constructor. When creating references to the
    /// CsoundUnity object make sure to create them in the scripts Awake() function.
    /// </summary>
    void Awake()
    {
        initialized = false;

        AudioSettings.GetDSPBufferSize(out bufferSize, out numBuffers);

        dataPath = Path.GetFullPath(Path.Combine("Packages", packageName, "Runtime"));

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        dataPath = Path.Combine(dataPath, "Win64"); // Csound plugin libraries in Windows Editor
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        dataPath = Path.Combine(dataPath, "macOS");
#elif UNITY_ANDROID
        dataPath = Path.Combine(dataPath, "Android");
#endif

        //string path = System.Environment.GetEnvironmentVariable("Path");
        //if (string.IsNullOrWhiteSpace(path) || !path.Contains(dataPath))
        //{
        //    string updatedPath = path + ";" + dataPath;
        //    print("Updated path:" + updatedPath);
        //    System.Environment.SetEnvironmentVariable("Path", updatedPath); // Is this needed for Csound to find libraries?
        //}


        audioSource = GetComponent<AudioSource>();
        audioSource.spatializePostEffects = true;

        // FIX SPATIALIZATION ISSUES
        if (audioSource.clip == null && !processClipAudio)
        {
            var ac = AudioClip.Create("DummyClip", 32, 1, AudioSettings.outputSampleRate, false);
            var data = new float[32];
            for (var i = 0; i < data.Length; i++) data[i] = 1;
            ac.SetData(data, 0);

            audioSource.clip = ac;
            audioSource.loop = true;
            audioSource.Play();
        }


        Debug.Log("CsoundUnity done init, compiledOk? " + compiledOk);


        //
        initialized = true;

    }


    #region PUBLIC_METHODS

    /// <summary>
    /// Returns the Csound version number times 1000 (5.00.0 = 5000).
    /// </summary>
    /// <returns></returns>
    public int GetVersion()
    {
        return csound.GetVersion();
    }

    /// <summary>
    /// Returns the Csound API version number times 100 (1.00 = 100).
    /// </summary>
    /// <returns></returns>
    public int GetAPIVersion()
    {
        return csound.GetAPIVersion();
    }

    /// <summary>
    /// Returns true if the csd file was compiled without errors.
    /// </summary>
    /// <returns></returns>
    public bool CompiledWithoutError()
    {
        return compiledOk;
    }

    #region PERFORMANCE

    /// <summary>
    /// Sets the csd file 
    /// </summary>
    /// <param name="guid">the guid of the csd file asset</param>
    public void SetCsd(string guid)
    {
#if UNITY_EDITOR //for now setting csd is permitted from editor only, via asset guid

        // Debug.Log($"SET CSD guid: {guid}");
        if (string.IsNullOrWhiteSpace(guid) || !Guid.TryParse(guid, out Guid guidResult))
        {
            Debug.LogWarning($"GUID NOT VALID, Resetting fields");
            ResetFields();
            return;
        }

        var fileName = AssetDatabase.GUIDToAssetPath(guid);

        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName.Length < 4 ||
            Path.GetFileName(fileName).Length < 4 ||
            !Path.GetFileName(fileName).EndsWith(".csd"))
        {
            Debug.LogWarning("FILENAME not valid, Resetting fields");
            ResetFields();
            return;
        }

        this._csoundFileGUID = guid;
        this._csoundFileName = Path.GetFileName(fileName);
        var csoundFilePath = Path.GetFullPath(fileName);
        this._csoundAsset = (DefaultAsset)(AssetDatabase.LoadAssetAtPath(fileName, typeof(DefaultAsset)));
        this._csoundString = File.ReadAllText(csoundFilePath);
        this._channels = ParseCsdFile(fileName);
        var count = 0;
        foreach (var chan in this._channels)
            if(!_channelsIndexDict.ContainsKey(chan.channel))
                _channelsIndexDict.Add(chan.channel, count++);
        this._availableAudioChannels = ParseCsdFileForAudioChannels(fileName);

        foreach (var name in availableAudioChannels)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (!namedAudioChannelDataDict.ContainsKey(name))
            {
                namedAudioChannelDataDict.Add(name, new MYFLT[bufferSize]);
                namedAudioChannelTempBufferDict.Add(name, new MYFLT[ksmps]);
            }
        }

#endif
    }

    /// <summary>
    /// Parse, and compile the given orchestra from an ASCII string,
    /// also evaluating any global space code (i-time only)
    /// this can be called during performance to compile a new orchestra.
    /// <example>
    /// This sample shows how to use CompileOrc
    /// <code>
    /// string orc = "instr 1 \n a1 rand 0dbfs/4 \n out a1 \nendin\n";
    /// CompileOrc(orc);
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="orcStr"></param>
    /// <returns></returns>
    public int CompileOrc(string orcStr)
    {
        return csound.CompileOrc(orcStr);
    }

    /// <summary>
    /// Send a score event to Csound in the form of "i1 0 10 ....
    /// </summary>
    /// <param name="scoreEvent">the score string to send</param>
    public void SendScoreEvent(string scoreEvent)
    {
        //print(scoreEvent);
        csound.SendScoreEvent(scoreEvent);
    }

    /// <summary>
    /// Rewinds a compiled Csound score to the time specified with SetScoreOffsetSeconds().
    /// </summary>
    public void RewindScore()
    {
        csound.RewindScore();
    }

    /// <summary>
    /// Csound score events prior to the specified time are not performed,
    /// and performance begins immediately at the specified time
    /// (real-time events will continue to be performed as they are received).
    /// Can be used by external software, such as a VST host, to begin score performance midway through a Csound score,
    /// for example to repeat a loop in a sequencer, or to synchronize other events with the Csound score.
    /// </summary>
    /// <param name="value"></param>
    public void SetScoreOffsetSeconds(MYFLT value)
    {
        csound.CsoundSetScoreOffsetSeconds(value);
    }

    /// <summary>
    /// Get the current sample rate
    /// </summary>
    /// <returns></returns>
    public MYFLT GetSr()
    {
        return csound.GetSr();
    }

    /// <summary>
    /// Get the current control rate
    /// </summary>
    /// <returns></returns>
    public MYFLT GetKr()
    {
        return csound.GetKr();
    }

    /// <summary>
    /// Process a ksmps-sized block of samples
    /// </summary>
    /// <returns></returns>
    public int PerformKsmps()
    {
        return csound.PerformKsmps();
    }

    /// <summary>
    /// Get the number of audio sample frames per control sample.
    /// </summary>
    /// <returns></returns>
    public uint GetKsmps()
    {
        return csound.GetKsmps();
    }

    #endregion PERFORMANCE

    #region CSD_PARSE

    /// <summary>
    /// Parse the csd and returns available audio channels (set in csd via: <code>chnset avar, "audio channel name") </code>
    /// </summary>
    /// <param name="filename"></param>
    /// <returns></returns>
    public static List<string> ParseCsdFileForAudioChannels(string filename)
    {
        if (!File.Exists(filename)) return null;

        string[] fullCsdText = File.ReadAllLines(filename);
        if (fullCsdText.Length < 1) return null;

        List<string> locaAudioChannels = new List<string>();

        foreach (string line in fullCsdText)
        {
            var trimmd = line.TrimStart();
            if (!trimmd.Contains("chnset")) continue;
            if (trimmd.StartsWith(";")) continue;
            var lndx = trimmd.IndexOf("chnset");
            var chnsetEnd = lndx + "chnset".Length + 1;
            var prms = trimmd.Substring(chnsetEnd, trimmd.Length - chnsetEnd);
            var split = prms.Split(',');
            if (!split[0].StartsWith("a") && !split[0].StartsWith("ga"))
                continue; //discard non audio variables
            // Debug.Log("found audio channel");
            var ach = split[1].Replace('\\', ' ').Replace('\"', ' ').Trim();
            if (!locaAudioChannels.Contains(ach))
                locaAudioChannels.Add(ach);
        }
        return locaAudioChannels;
    }

    /// <summary>
    /// Parse the csd file
    /// </summary>
    /// <param name="filename">the csd file to parse</param>
    /// <returns></returns>
    public static List<CsoundChannelController> ParseCsdFile(string filename)
    {
        if (!File.Exists(filename)) return null;

        string[] fullCsdText = File.ReadAllLines(filename);
        if (fullCsdText.Length < 1) return null;

        List<CsoundChannelController> locaChannelControllers;
        locaChannelControllers = new List<CsoundChannelController>();

        foreach (string line in fullCsdText)
        {

            if (line.Contains("</"))
                break;

            var trimmd = line.TrimStart();
            //discard csound comments in cabbage widgets
            if (trimmd.StartsWith(";"))
            {
                //Debug.Log("discarding "+line);
                continue;
            }
            string newLine = trimmd;
            string control = trimmd.Substring(0, trimmd.IndexOf(" ") > -1 ? trimmd.IndexOf(" ") : 0);
            if (control.Length > 0)
                newLine = newLine.Replace(control, "");

            if (control.Contains("slider") || control.Contains("button") || control.Contains("checkbox")
                || control.Contains("groupbox") || control.Contains("form") || control.Contains("combobox"))
            {
                CsoundChannelController controller = new CsoundChannelController();
                controller.type = control;

                if (trimmd.IndexOf("caption(") > -1)
                {
                    string infoText = trimmd.Substring(trimmd.IndexOf("caption(") + 9);
                    infoText = infoText.Substring(0, infoText.IndexOf(")") - 1);
                    controller.caption = infoText;
                }

                if (trimmd.IndexOf("text(") > -1)
                {
                    string text = trimmd.Substring(trimmd.IndexOf("text(") + 6);
                    text = text.Substring(0, text.IndexOf(")") - 1);
                    text = text.Replace("\"", "");
                    text = text.Replace('"', new char());
                    if (controller.type == "combobox") //if combobox, create a range
                    {
                        char[] delimiterChars = { ',' };
                        string[] tokens = text.Split(delimiterChars);
                        controller.SetRange(1, tokens.Length, 0);

                        for (var o = 0; o < tokens.Length; o++)
                        {
                            tokens[o] = string.Join("", tokens[o].Split(default(string[]), System.StringSplitOptions.RemoveEmptyEntries));
                        }
                        controller.options = tokens;
                    }
                }

                if (trimmd.IndexOf("items(") > -1)
                {
                    string text = trimmd.Substring(trimmd.IndexOf("items(") + 7);
                    text = text.Substring(0, text.IndexOf(")") - 1);
                    //TODO THIS OVERRIDES TEXT!
                    text = text.Replace("\"", "");
                    text = text.Replace('"', new char());
                    if (controller.type == "combobox")
                    {
                        char[] delimiterChars = { ',' };
                        string[] tokens = text.Split(delimiterChars);
                        controller.SetRange(1, tokens.Length, 0);

                        for (var o = 0; o < tokens.Length; o++)
                        {
                            tokens[o] = string.Join("", tokens[o].Split(default(string[]), System.StringSplitOptions.RemoveEmptyEntries));
                        }
                        controller.options = tokens;
                    }
                }

                if (trimmd.IndexOf("channel(") > -1)
                {
                    string channel = trimmd.Substring(trimmd.IndexOf("channel(") + 9);
                    channel = channel.Substring(0, channel.IndexOf(")") - 1);
                    controller.channel = channel;
                }

                if (trimmd.IndexOf("range(") > -1)
                {
                    int rangeAt = trimmd.IndexOf("range(");
                    if (rangeAt != -1)
                    {
                        string range = trimmd.Substring(rangeAt + 6);
                        range = range.Substring(0, range.IndexOf(")"));
                        char[] delimiterChars = { ',' };
                        string[] tokens = range.Split(delimiterChars);
                        for (var i = 0; i < tokens.Length; i++)
                        {
                            tokens[i] = string.Join("", tokens[i].Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
                            if (tokens[i].StartsWith("."))
                            {
                                tokens[i] = "0" + tokens[i];
                            }
                            if (tokens[i].StartsWith("-."))
                            {
                                tokens[i] = "-0" + tokens[i].Substring(2, tokens[i].Length - 2);
                            }
                        }
                        var min = float.Parse(tokens[0], CultureInfo.InvariantCulture);
                        var max = float.Parse(tokens[1], CultureInfo.InvariantCulture);
                        var val = float.Parse(tokens[2], CultureInfo.InvariantCulture);
                        controller.SetRange(min, max, val);
                    }
                }

                if (line.IndexOf("value(") > -1)
                {
                    string value = trimmd.Substring(trimmd.IndexOf("value(") + 6);
                    value = value.Substring(0, value.IndexOf(")"));
                    value = value.Replace("\"", "");
                    controller.value = value.Length > 0 ? float.Parse(value, CultureInfo.InvariantCulture) : 0;
                    if (control.Contains("combobox"))
                    {
                        //Cabbage combobox index starts from 1
                        controller.value = controller.value - 1;
                        // Debug.Log("combobox value in parse: " + controller.value);
                    }
                }
                locaChannelControllers.Add(controller);
            }
        }
        return locaChannelControllers;
    }

    #endregion CSD_PARSE

    #region IO_BUFFERS

    /// <summary>
    /// Set a sample in Csound's input buffer
    /// </summary>
    /// <param name="frame"></param>
    /// <param name="channel"></param>
    /// <param name="sample"></param>
    public void SetInputSample(int frame, int channel, MYFLT sample)
    {
        csound.SetSpinSample(frame, channel, sample);
    }

    /// <summary>
    /// Adds the indicated sample into the audio input working buffer (spin);
    /// this only ever makes sense before calling PerformKsmps().
    /// The frame and channel must be in bounds relative to ksmps and nchnls.
    /// NB: the spin buffer needs to be cleared at every k-cycle by calling ClearSpin().
    /// </summary>
    /// <param name="frame"></param>
    /// <param name="channel"></param>
    /// <param name="sample"></param>
    public void AddInputSample(int frame, int channel, MYFLT sample)
    {
        csound.AddSpinSample(frame, channel, sample);
    }

    /// <summary>
    /// Clears the input buffer (spin).
    /// </summary>
    public void ClearSpin()
    {
        if (csound != null)
        {
            Debug.Log("clear spin");
            csound.ClearSpin();
        }
    }

    /// <summary>
    /// Get a sample from Csound's audio output buffer
    /// </summary>
    /// <param name="frame"></param>
    /// <param name="channel"></param>
    /// <returns></returns>
    public MYFLT GetOutputSample(int frame, int channel)
    {
        return csound.GetSpoutSample(frame, channel);
    }

    /// <summary>
    /// Get Csound's audio input buffer
    /// </summary>
    /// <returns></returns>
    public MYFLT[] GetSpin()
    {
        return csound.GetSpin();
    }

    /// <summary>
    /// Get Csound's audio output buffer
    /// </summary>
    /// <returns></returns>
    public MYFLT[] GetSpout()
    {
        return csound.GetSpout();
    }

    #endregion IO_BUFFERS

    #region CONTROL_CHANNELS
    /// <summary>
    /// Sets a Csound channel. Used in connection with a chnget opcode in your Csound instrument.
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="val"></param>
    public void SetChannel(string channel, MYFLT val)
    {
        if(_channelsIndexDict.ContainsKey(channel))
            channels[_channelsIndexDict[channel]].value = (float)val;
        csound.SetChannel(channel, val);
    }

    /// <summary>
    /// Sets a string channel in Csound. Used in connection with a chnget opcode in your Csound instrument.
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="val"></param>
    public void SetStringChannel(string channel, string val)
    {
        csound.SetStringChannel(channel, val);
    }

    /// <summary>
    /// Gets a Csound channel. Used in connection with a chnset opcode in your Csound instrument.
    /// </summary>
    /// <param name="channel"></param>
    /// <returns></returns>
    public MYFLT GetChannel(string channel)
    {
        return csound.GetChannel(channel);
    }

    /// <summary>
    /// blocking method to get a list of the channels from Csound, not from the serialized list of this instance
    /// </summary>
    /// <returns></returns>
    public IDictionary<string, CsoundUnityBridge.ChannelInfo> GetChannelList()
    {
        return csound.GetChannelList();
    }

    #endregion CONTROL_CHANNELS

    #region AUDIO_CHANNELS

    /// <summary>
    /// Gets a Csound Audio channel. Used in connection with a chnset opcode in your Csound instrument.
    /// </summary>
    /// <param name="channel"></param>
    /// <returns></returns>
    public MYFLT[] GetAudioChannel(string channel)
    {
        return csound.GetAudioChannel(channel);
    }

    #endregion AUDIO_CHANNELS

    #region TABLES

    /// <summary>
    /// Creates a table with the supplied samples.
    /// Can be called during performance.
    /// </summary>
    /// <param name="tableNumber">The table number</param>
    /// <param name="samples"></param>
    /// <returns></returns>
    public int CreateTable(int tableNumber, MYFLT[] samples/*, int nChannels*/)
    {
        if (samples.Length < 1) return -1;
        var resTable = CreateTableInstrument(tableNumber, samples.Length);
        if (resTable != 0)
            return -1;
        // copy samples to the newly created table
        CopyTableIn(tableNumber, samples);

        return resTable;
    }

    /// <summary>
    /// Creates an empty table, to be filled with samples later. 
    /// Please note that trying to read the samples from an empty folder will produce a crash.
    /// Can be called during performance.
    /// </summary>
    /// <param name="tableNumber">The number of the newly created table</param>
    /// <param name="tableLength">The length of the table in samples</param>
    /// <returns>0 If the table could be created</returns>
    public int CreateTableInstrument(int tableNumber, int tableLength/*, int nChannels*/)
    {
        string createTableInstrument = String.Format(@"gisampletable{0} ftgen {0}, 0, {1}, -7, 0, 0", tableNumber, -tableLength /** AudioSettings.outputSampleRate*/);
        // Debug.Log("orc to create table: \n" + createTableInstrument);
        return CompileOrc(createTableInstrument);
    }

    /// <summary>
    /// Returns the length of a function table (not including the guard point), or -1 if the table does not exist.
    /// </summary>
    /// <param name="table"></param>
    /// <returns></returns>
    public int GetTableLength(int table)
    {
        return csound.TableLength(table);
    }

    /// <summary>
    /// Retrieves a single sample from a Csound function table.
    /// </summary>
    /// <param name="tableNumber"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    public MYFLT GetTableSample(int tableNumber, int index)
    {
        return csound.GetTable(tableNumber, index);
    }

    /// <summary>
    /// Stores values to function table 'numTable' in tableValues, and returns the table length (not including the guard point).
    /// If the table does not exist, tableValues is set to NULL and -1 is returned.
    /// </summary>
    /// <param name="tableValues"></param>
    /// <param name="numTable"></param>
    /// <returns></returns>
    public int GetTable(out MYFLT[] tableValues, int numTable)
    {
        return csound.GetTable(out tableValues, numTable);
    }

    /// <summary>
    /// Stores the arguments used to generate function table 'tableNum' in args, and returns the number of arguments used.
    /// If the table does not exist, args is set to NULL and -1 is returned.
    /// NB: the argument list starts with the GEN number and is followed by its parameters.
    /// eg. f 1 0 1024 10 1 0.5 yields the list {10.0,1.0,0.5}
    /// </summary>
    /// <param name="args"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    public int GetTableArgs(out MYFLT[] args, int index)
    {
        return csound.GetTableArgs(out args, index);
    }

    /// <summary>
    /// Sets the value of a slot in a function table. The table number and index are assumed to be valid.
    /// </summary>
    /// <param name="table"></param>
    /// <param name="index"></param>
    /// <param name="value"></param>
    public void SetTable(int table, int index, MYFLT value)
    {
        csound.SetTable(table, index, value);
    }

    /// <summary>
    /// Copy the contents of a function table into a supplied array dest
    /// The table number is assumed to be valid, and the destination needs to have sufficient space to receive all the function table contents.
    /// </summary>
    /// <param name="table"></param>
    /// <param name="dest"></param>
    public void CopyTableOut(int table, out MYFLT[] dest)
    {
        csound.TableCopyOut(table, out dest);
    }

    /// <summary>
    /// Asynchronous version of copyTableOut
    /// </summary>
    /// <param name="table"></param>
    /// <param name="dest"></param>
    public void CopyTableOutAsync(int table, out MYFLT[] dest)
    {
        csound.TableCopyOutAsync(table, out dest);
    }

    /// <summary>
    /// Copy the contents of a supplied array into a function table
    /// The table number is assumed to be valid, and the destination needs to have sufficient space to receive all the function table contents.
    /// </summary>
    /// <param name="table">the number of the table</param>
    /// <param name="source">the supplied array</param>
    public void CopyTableIn(int table, MYFLT[] source)
    {
        csound.TableCopyIn(table, source);
    }

    /// <summary>
    /// Asynchronous version of copyTableOut
    /// </summary>
    /// <param name="table"></param>
    /// <param name="source"></param>
    public void CopyTableInAsync(int table, MYFLT[] source)
    {
        csound.TableCopyInAsync(table, source);
    }

    /// <summary>
    /// Checks if a given GEN number num is a named GEN if so, it returns the string length (excluding terminating NULL char)
    /// Otherwise it returns 0.
    /// </summary>
    /// <param name="num"></param>
    /// <returns></returns>
    public int IsNamedGEN(int num)
    {
        return csound.IsNamedGEN(num);
    }

    /// <summary>
    /// Gets the GEN name from a number num, if this is a named GEN
    /// The final parameter is the max len of the string (excluding termination)
    /// </summary>
    /// <param name="num"></param>
    /// <param name="name"></param>
    /// <param name="len"></param>
    public void GetNamedGEN(int num, out string name, int len)
    {
        csound.GetNamedGEN(num, out name, len);
    }

    /// <summary>
    /// Returns a Dictionary keyed by the names of all named table generators.
    /// Each name is paired with its internal function number.
    /// </summary>
    /// <returns></returns>
    public IDictionary<string, int> GetNamedGens()
    {
        return csound.GetNamedGens();
    }

    #endregion TABLES

    #region CALLBACKS

    public void SetYieldCallback(Action callback)
    {

        csound.SetYieldCallback(callback);
    }

    public void SetSenseEventCallback<T>(Action<T> action, T type) where T : class
    {
        csound.SetSenseEventCallback(action, type);
    }

    public void AddSenseEventCallback(CsoundUnityBridge.Csound6SenseEventCallbackHandler callbackHandler)
    {
        csound.SenseEventsCallback += callbackHandler;//Csound_SenseEventsCallback;
    }

    public void RemoveSenseEventCallback(CsoundUnityBridge.Csound6SenseEventCallbackHandler callbackHandler)
    {
        csound.SenseEventsCallback -= callbackHandler;
    }

    #endregion CALLBACKS

    #region UTILITIES

#if UNITY_EDITOR
    /// <summary>
    /// A method that retrieves the current csd file path from its GUID
    /// </summary>
    /// <returns></returns>
    public string GetFilePath()
    {
        return Path.Combine(Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length), AssetDatabase.GUIDToAssetPath(csoundFileGUID));
    }
#endif

    public CsoundUnityBridge.CSOUND_PARAMS GetParams()
    {
        return csound.GetParams();
    }

    public void SetParams(CsoundUnityBridge.CSOUND_PARAMS parms)
    {
        csound.SetParams(parms);
    }

    /// <summary>
    /// Get Environment path
    /// </summary>
    /// <param name="envType">the type of the environment to get</param>
    /// <returns></returns>
    public string GetEnv(EnvType envType)
    {
        return csound.GetEnv(envType.ToString());
    }

    //#if CSHARP_7_3_OR_NEWER
    //    /// <summary>
    //    /// Get the Opcode List, async
    //    /// </summary>
    //    /// <returns></returns>
    //    public async Task<IDictionary<string, IList<CsoundUnityBridge.OpcodeArgumentTypes>>> GetOpcodeListAsync()
    //    {
    //        return await csound.GetOpcodeListAsync();
    //    }
    //#endif

    /// <summary>
    /// Get the Opcode List, blocking
    /// </summary>
    /// <returns></returns>
    public IDictionary<string, IList<CsoundUnityBridge.OpcodeArgumentTypes>> GetOpcodeList()
    {
        return csound.GetOpcodeList();
    }

    /// <summary>
    /// Get the number of input channels
    /// </summary>
    /// <returns></returns>
    public uint GetNchnlsInputs()
    {
        return csound.GetNchnlsInput();
    }

    /// <summary>
    /// Get the number of output channels
    /// </summary>
    /// <returns></returns>
    public uint GetNchnls()
    {
        return csound.GetNchnls();
    }

    /// <summary>
    /// Get 0 dbfs
    /// </summary>
    /// <returns></returns>
    public MYFLT Get0dbfs()
    {
        return csound.Get0dbfs();
    }

    /// <summary>
    /// Returns the current performance time in samples
    /// </summary>
    /// <returns></returns>
    public long GetCurrentTimeSamples()
    {
        return csound.GetCurrentTimeSamples();
    }

    /// <summary>
    /// map MYFLT within one range to another
    /// </summary>
    /// <param name="value"></param>
    /// <param name="from1"></param>
    /// <param name="to1"></param>
    /// <param name="from2"></param>
    /// <param name="to2"></param>
    /// <returns></returns>
    public static float Remap(float value, float from1, float to1, float from2, float to2, bool clamp = false)
    {
        float retValue = (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        if (float.IsNaN(retValue)) return from2;
        if (float.IsPositiveInfinity(retValue)) return to2;
        if (float.IsNegativeInfinity(retValue)) return from2;
        return clamp ? Mathf.Clamp(retValue, from2, to2) : retValue;
    }

    /// <summary>
    /// Get Samples from a path, specifying the origin of the path. This will return an interleaved
    /// array of samples, with the first index used to specify the number of channels. This array can
    /// be passed to the CsoundUnity.CreateTable() method for processing by Csound. Use async versions to
    /// to load very large files.
    /// 
    /// Note: You need to be careful that your AudioClips match the SR of the 
    /// project. If not, you will hear some re-pitching issues with your audio when
    /// you play it back with a table reader opcode. 
    /// </summary>
    /// <param name="source"></param>
    /// <param name="origin"></param>
    /// <param name="async"></param>
    /// <returns></returns>
    /// 
    public static MYFLT[] GetStereoSamples(string source, SamplesOrigin origin)
    {
        return GetSamples(source, origin, 0, true);
    }

    public static MYFLT[] GetMonoSamples(string source, SamplesOrigin origin, int channelNumber)
    {
        return GetSamples(source, origin, channelNumber, true);
    }
    public static MYFLT[] GetSamples(string source, SamplesOrigin origin, int channelNumber = 1, bool writeChannelData = false)
    {
        MYFLT[] res = new MYFLT[0];
        switch (origin)
        {
            case SamplesOrigin.Resources:
                var src = Resources.Load<AudioClip>(source);
                if (src == null)
                {
                    res = null;
                    break;
                }
                var data = new float[src.samples * src.channels];
                src.GetData(data, 0);

                if (writeChannelData)
                {
                    res = new MYFLT[src.samples * src.channels + 1];
                    res[0] = src.channels;
                    var s = 1;
                    for (var i = 0; i < data.Length; i++)
                    {
                        res[s] = data[i];
                        s++;
                    }
                }
                else
                {
                    var s = 0;
                    res = new MYFLT[src.samples];

                    for (var i = 0; i < data.Length; i += src.channels, s++)
                    {
                        res[s] = data[i + (channelNumber - 1)];
                    }
                }
                break;
            case SamplesOrigin.StreamingAssets:
                Debug.LogWarning("Not implemented yet");
                break;
            case SamplesOrigin.Absolute:
                Debug.LogWarning("Not implemented yet");
                break;
        }

        return res;
    }

    /// <summary>
    /// Async version of GetSamples
    /// example of usage:
    /// <code>
    /// yield return CsoundUnity.GetSamples(source.name, CsoundUnity.SamplesOrigin.Resources, (samples) =>
    /// {
    ///     Debug.Log("samples loaded: "+samples.Length+", creating table");
    ///     csound.CreateTable(100, samples);
    /// });
    /// </code>
    /// </summary>
    /// <param name="source">the name of the AudioClip to load</param>
    /// <param name="origin">the origin of the path</param>
    /// <param name="onSamplesLoaded">the callback when samples are loaded</param>
    /// <returns></returns>
    public static IEnumerator GetSamples(string source, SamplesOrigin origin, Action<MYFLT[]> onSamplesLoaded)
    {
        switch (origin)
        {
            case SamplesOrigin.Resources:
                //var src = Resources.Load<AudioClip>(source);
                var req = Resources.LoadAsync<AudioClip>(source);

                while (!req.isDone)
                {
                    yield return null;
                }
                var samples = ((AudioClip)req.asset).samples;
                if (samples == 0)
                {
                    onSamplesLoaded?.Invoke(null);
                    yield break;
                }
                //Debug.Log("src.samples: " + samples);
                var ac = ((AudioClip)req.asset);
                var data = new float[samples * ac.channels];
                ac.GetData(data, 0);
                MYFLT[] res = new MYFLT[samples * ac.channels];
                var s = 0;
                foreach (var d in data)
                {
                    res[s] = (MYFLT)d;
                    s++;
                }
                onSamplesLoaded?.Invoke(res);
                break;
            case SamplesOrigin.StreamingAssets:
                Debug.LogWarning("Not implemented yet");
                break;
            case SamplesOrigin.Absolute:
                Debug.LogWarning("Not implemented yet");
                break;
        }


    }

#if UNITY_ANDROID

    /**
     * Android method to write csd file to a location it can be read from Method returns the file path. 
     */
    public string GetCsoundFile(string csoundFileContents)
    {
        try
        {
            Debug.Log("Csound file contents:");
            Debug.Log(csoundFileContents);
            string filename = Application.persistentDataPath + "/csoundFile.csd";
            Debug.Log("Writing to " + filename);

            if (!File.Exists(filename))
            {
                Debug.Log("File doesnt exist, creating it");
                File.Create(filename).Close();
            }

            if (File.Exists(filename))
            {
                Debug.Log("File has been created");
            }

            File.WriteAllText(filename, csoundFileContents);
            return filename;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error writing to file: " + e.ToString());
        }

        return "";
    }

    public void GetCsoundAudioFile(byte[] data, string filename)
    {
        try
        {
            string name = Application.persistentDataPath + "/" + filename;
            File.Create(name).Close();
            File.WriteAllBytes(name, data);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error writing to file: " + e.ToString());
        }
    }
#endif

    #endregion UTILITIES

    #endregion PUBLIC_METHODS


    #region ENUMS

    /// <summary>
    /// The enum representing the Csound Environment Variables
    /// 
    /// </summary>
    public enum EnvType
    {
        /// <summary>
        /// Default directory for sound files.
        /// Used if no full path is given for sound files.
        /// </summary>
        SFDIR,

        /// <summary>
        /// Default directory for input (source) audio and MIDI files.
        /// Used if no full path is given for sound files.
        /// May be used in conjunction with SFDIR to set separate input and output directories.
        /// Please note that MIDI files as well as audio files are also sought inside SSDIR.
        /// </summary>
        SSDIR,

        /// <summary>
        /// Default directory for analysis files.
        /// Used if no full path is given for analysis files.
        /// </summary>
        SADIR,

        /// <summary>
        /// Sets the default output file type.
        /// Currently only 'WAV', 'AIFF' and 'IRCAM' are valid.
        /// This flag is checked by the csound executable and the utilities and is used if no file output type is specified.
        /// </summary>
        SFOUTYP,

        /// <summary>
        /// Include directory. Specifies the location of files used by #include statements.
        /// </summary>
        INCDIR,

        /// <summary>
        /// Defines the location of csound opcode plugins for the single precision float (32-bit) version.
        /// </summary>
        OPCODE6DIR,

        /// <summary>
        /// Defines the location of csound opcode plugins for the double precision float (64-bit) version.
        /// </summary>
        OPCODE6DIR64,

        /// <summary>
        /// Is used by the FLTK widget opcodes when loading and saving snapshots.
        /// </summary>
        SNAPDIR,

        /// <summary>
        /// Defines the csound resource (or configuration) file.
        /// A full path and filename containing csound flags must be specified.
        /// This variable defaults to .csoundrc if not present.
        /// </summary>
        CSOUNDRC,
        /// <summary>
        /// In Csound 5.00 and later versions,
        /// the localisation of messages is controlled by two environment variables CSSTRNGS and CS_LANG,
        /// both of which are optional.
        /// CSSTRNGS points to a directory containing .xmg files.
        /// </summary>
        CSSTRNGS,

        /// <summary>
        /// Selects a language for csound messages.
        /// </summary>
        CS_LANG,

        /// <summary>
        /// Is used by the STK opcodes to find the raw wave files.
        /// Only relevant if you are using STK wrapper opcodes like STKBowed or STKBrass.
        /// </summary>
        RAWWAVE_PATH,

        /// <summary>
        /// If this environment variable is set to "yes",
        /// then any graph displays are closed automatically at the end of performance
        /// (meaning that you possibly will not see much of them in the case of a short non-realtime render).
        /// Otherwise, you need to click "Quit" in the FLTK display window to exit,
        /// allowing for viewing the graphs even after the end of score is reached.
        /// </summary>
        CSNOSTOP,

        /// <summary>
        /// Default directory for MIDI files.
        /// Used if no full path is given for MIDI files.
        /// Please note that MIDI files are sought in SSDIR and SFDIR as well.
        /// </summary>
        MFDIR,

        /// <summary>
        /// Allows defining a list of plugin libraries that should be skipped.
        /// Libraries can be separated with commas, and don't require the "lib" prefix.
        /// </summary>
        CS_OMIT_LIBS
    }

    /// <summary>
    /// Where the samples to load come from:
    /// the Resources folder
    /// the StreamingAssets folder
    /// An absolute path, can be external of the Unity Project
    /// </summary>
    public enum SamplesOrigin { Resources, StreamingAssets, Absolute }

    #endregion ENUMS


    #region PRIVATE_METHODS

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (csound != null)
        {
            ProcessBlock(data, channels);
        }
    }

    /// <summary>
    /// Processes a block of samples
    /// </summary>
    /// <param name="samples"></param>
    /// <param name="numChannels"></param>
    private void ProcessBlock(float[] samples, int numChannels)
    {
        if (compiledOk && initialized && !_quitting)
        {
            for (int i = 0; i < samples.Length; i += numChannels, ksmpsIndex++)
            {
                for (uint channel = 0; channel < numChannels; channel++)
                {
                    // necessary to avoid calling csound functions when quitting while reading this block of samples
                    // always remember OnAudioFilterRead runs on a different thread
                    if (_quitting) return;

                    if (mute == true)
                        samples[i + channel] = 0.0f;
                    else
                    {
                        if ((ksmpsIndex >= GetKsmps()) && (GetKsmps() > 0))
                        {
                            var res = PerformKsmps();
                            performanceFinished = res == 1;
                            ksmpsIndex = 0;

                            foreach (var chanName in availableAudioChannels)
                            {
                                if (!namedAudioChannelTempBufferDict.ContainsKey(chanName)) continue;
                                namedAudioChannelTempBufferDict[chanName] = GetAudioChannel(chanName);
                            }
                        }

                        if (processClipAudio)
                        {
                            SetInputSample((int)ksmpsIndex, (int)channel, samples[i + channel] * zerdbfs);
                        }

                        //if csound nChnls are more than the current channel, set the last csound channel available on the sample (assumes GetNchnls above 0)
                        var outputSampleChannel = channel < GetNchnls() ? channel : GetNchnls() - 1;
                        var output = (float)GetOutputSample((int)ksmpsIndex, (int)outputSampleChannel) / zerdbfs;
                        // multiply Csound output by the sample value to maintain spatialization set by Unity. 
                        // don't multiply if reading from a clip: this should maintain the spatialization of the clip anyway
                        samples[i + channel] = processClipAudio ? output : samples[i + channel] * output;
                        // samples[i + channel] = output;

                        if (loudVolumeWarning && (samples[i + channel] > loudWarningThreshold))
                        {
                            samples[i + channel] = 0.0f;
                            Debug.LogWarning("Volume is too high! Clearing output");
                        }
                    }
                }

                // update the audioChannels just when this instance is not muted
                if (!mute)
                    foreach (var chanName in availableAudioChannels)
                    {
                        if (!namedAudioChannelDataDict.ContainsKey(chanName) || !namedAudioChannelTempBufferDict.ContainsKey(chanName)) continue;
                        namedAudioChannelDataDict[chanName][i / numChannels] = namedAudioChannelTempBufferDict[chanName][ksmpsIndex];
                    }
            }
        }
    }

    /// <summary>
    /// Print the Csound output to the Unity message console.
    /// No need to call this manually, it is set up and controlled in the CsoundUnity Awake() function.
    /// </summary>
    void LogCsoundMessages()
    {
        //print Csound message to Unity console....
        for (int i = 0; i < csound.GetCsoundMessageCount(); i++)
            print(csound.GetCsoundMessage());
    }

    /// <summary>
    /// A logging routine, checks for Csound messages every 'interval' seconds
    /// </summary>
    /// <param name="interval"></param>
    /// <returns></returns>
    IEnumerator Logging(float interval)
    {
        while (true)
        {
            while (this.logCsoundOutput)
            {
                for (int i = 0; i < csound.GetCsoundMessageCount(); i++)
                {
                    if (this.logCsoundOutput)    // exiting when csound messages are very high in number 
                    {
                        print(csound.GetCsoundMessage());
                        yield return null;          //avoids Unity stuck on performance end
                    }
                }
                yield return new WaitForSeconds(interval);
            }
            yield return null; //wait one frame
        }
    }

    /// <summary>
    /// Resets all internal memory and state in preparation for a new performance. 
    /// Enables external software to run successive Csound performances without reloading Csound. 
    /// Implies csoundCleanup(), unless already called.
    /// </summary>
    public void CsoundReset()
    {
        csound.Reset();
    }

    /// <summary>
    /// Prints information about the end of a performance, and closes audio and MIDI devices. 
    /// Note: after calling csoundCleanup(), the operation of the perform functions is undefined.
    /// </summary>
    public void Cleanup()
    {
        csound.Cleanup();
    }

    /// <summary>
    /// Reset the fields of this instance
    /// </summary>
    private void ResetFields()
    {
#if UNITY_EDITOR
        this._csoundAsset = null;
#endif

        this._csoundFileName = null;
        this._csoundString = null;
        this._csoundFileGUID = string.Empty;

        this._channels.Clear();
        this._availableAudioChannels.Clear();

        this.namedAudioChannelDataDict.Clear();
        this.namedAudioChannelTempBufferDict.Clear();
    }

    private bool _quitting = false;
    /// <summary>
    /// Called automatically when the game stops. Needed so that Csound stops when your game does
    /// </summary>
    void OnApplicationQuit()
    {
        _quitting = true;
        if (LoggingCoroutine != null)
            StopCoroutine(LoggingCoroutine);

        if (csound != null)
        {
            csound.OnApplicationQuit();
        }
    }

    #endregion PRIVATE_METHODS
}
