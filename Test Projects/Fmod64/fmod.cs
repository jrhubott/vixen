using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Timers;
using System.Threading;
using System.Windows.Forms;

namespace FMOD {
    public enum FadeDirection { None, On, Off };
    public class fmod {
        private _System m_system = null;
        // 1 system per device
        private int m_deviceIndex;

        static List<_System> m_systems = new List<_System>();
        static List<string> m_deviceList = new List<string>();
        static int m_refCount = 0;

        List<SoundChannel> m_channels;
               
        static public fmod GetInstance(int deviceIndex) {
            if(m_systems.Count == 0) {
                _System system = null;

                CreateDeviceList();

                if(m_deviceList.Count > 0) {
                    // Create system instances, one for each device
                    int i;
                    for(i = 0; i < m_deviceList.Count; i++) {
                        if(Factory.System_Create(ref system) == RESULT.ERR_OUTPUT_INIT) {
                            // Remove all devices listed at this index and beyond
                            m_deviceList.RemoveRange(i, m_deviceList.Count - i);
                            break;
                        } else {
                            m_systems.Add(system);
                        }
                    }

                    // Initialize all system instances
                    string paramFile = Path.Combine(Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName), "sound.params");
                    int dspBufferCount = 0;
                    uint streamBufferSize = 32768;

                    // For some parameter tweaking, just in case
                    if(File.Exists(paramFile)) {
                        StreamReader reader = new StreamReader(paramFile);
                        string line;
                        string[] parts;
                        while((line = reader.ReadLine()) != null) {
                            parts = line.Split('=');
                            switch(parts[0].Trim().ToLower()) {
                                case "dspbuffercount":
                                    try {
                                        dspBufferCount = Convert.ToInt32(parts[1].Trim());
                                    } catch { }
                                    break;
                                case "streambuffersize":
                                    try {
                                        streamBufferSize = Convert.ToUInt32(parts[1].Trim());
                                    } catch { }
                                    break;
                            }
                        }
                        reader.Close();
                        reader.Dispose();
                    }

                    for(i = 0; i < m_systems.Count; i++) {
                        system = m_systems[i];

                        system.setDriver(i);

                        if(dspBufferCount > 0) {
                            // Must be called before init
                            system.setDSPBufferSize(1024, dspBufferCount);
                        }

                        system.init(32, INITFLAG.NORMAL, (IntPtr)null);
                        system.setStreamBufferSize(streamBufferSize, TIMEUNIT.RAWBYTES);
                    }
                }
            }

            // Return an instance with the appropriate system instance assignment
            if(deviceIndex == -1) deviceIndex = 0; // 0 is always primary device?
            if(deviceIndex < m_systems.Count) {
                m_refCount++;
                return new fmod(m_systems[deviceIndex], deviceIndex);
            } else {
                return new fmod(null, -1);
            }
        }

        static private void CreateDeviceList() {
            if(m_deviceList.Count > 0) return;

            // Create a bare system object to get the driver count
            _System system = null;
            Factory.System_Create(ref system);

            int driverCount = 0;
            system.getNumDrivers(ref driverCount);

            if(driverCount > 0) {
                // There are audio devices available

                // Create device list
                system.getNumDrivers(ref driverCount);
                StringBuilder sb = new StringBuilder(256);
                int i;
                for(i = 0; i < driverCount; i++) {
                    //system.getDriverName(i, sb, sb.Capacity);
                    GUID GUID = new GUID();
                    system.getDriverInfo(i, sb, sb.Capacity, ref GUID);
                    m_deviceList.Add(sb.ToString());
                }
            }

            system.release();
        }

        private fmod(_System system, int deviceIndex) {
            m_system = system;
            m_deviceIndex = deviceIndex;
            m_channels = new List<SoundChannel>();
        }

        public int DeviceIndex {
            get { return m_deviceIndex; }
            set {
                if(value == -1) value = 0;
                if(value < m_systems.Count) {
                    m_deviceIndex = value;
                    m_system = m_systems[value];
                }
            }
        }

        static public string[] GetSoundDeviceList() {
            CreateDeviceList();
            return m_deviceList.ToArray();
        }

        private string GetSoundName(Sound sound) {
            StringBuilder sb = new StringBuilder(256);
            sound.getName(sb, sb.Capacity);
            return sb.ToString().Trim();
        }

        public SoundChannel LoadSound(string fileName) {
            return LoadSound(fileName, null);
        }

        public SoundChannel LoadSound(string fileName, SoundChannel existingChannel) {
            if(m_system == null) return null;

            if(fileName == null || !File.Exists(fileName)) {
                return null;
            }

            Sound sound = null;
/*
  
           FMOD.CREATESOUNDEXINFO createSoundExInfo = new FMOD.CREATESOUNDEXINFO();

           createSoundExInfo.cbsize = Marshal.SizeOf(createSoundExInfo);

           //44,100 max frequency, so buffer that many samples
           createSoundExInfo.decodebuffersize = 44100;
  
           // Adding ref createSoundExInfo as an argument in createSound() was causing ERR_INVALID_SPEAKER in Vista x64
  
 */

            ERRCHECK(m_system.createSound(fileName, (FMOD.MODE._2D | FMOD.MODE.HARDWARE | FMOD.MODE.CREATESTREAM | MODE.ACCURATETIME), ref sound));
            if(existingChannel == null) {
                existingChannel = new SoundChannel(sound);
                m_channels.Add(existingChannel);
            } else {
                existingChannel.Sound = sound;
            }

            return existingChannel;
        }

        public void ReleaseSound(SoundChannel soundChannel) {
            if(soundChannel != null) {
                if(m_channels.Contains(soundChannel)) {
                    m_channels.Remove(soundChannel);
                }
                soundChannel.Dispose();
            }
        }

        public object[] LoadSoundStats(string fileName) {
            // returns [string name, uint length]
            if(fileName == null || !File.Exists(fileName)) return null;

            Sound sound = null;
            uint length = 0;
            string name;

            ERRCHECK(m_system.createSound(fileName, (FMOD.MODE._2D | FMOD.MODE.HARDWARE | FMOD.MODE.CREATESTREAM | MODE.ACCURATETIME), ref sound));
            sound.getLength(ref length, TIMEUNIT.MS);

            name = GetSoundName(sound);

            sound.release();
            return new object[] { name, length };
        }

        public void Play(SoundChannel soundChannel) {
            if(m_system == null) {
                throw new Exception("Cannot play a sound with no valid sound device");
            }
            PlaySound(soundChannel);
        }

        public void Play(SoundChannel soundChannel, bool paused) {
            PlaySound(soundChannel, paused);
        }

        private void PlaySound(SoundChannel soundChannel) {
            //*** testing
            //if(soundChannel == null) System.Windows.Forms.MessageBox.Show("Sound channel is null");
            //if(soundChannel.Sound == null) System.Windows.Forms.MessageBox.Show("Sound is null");
            //if(m_system == null) System.Windows.Forms.MessageBox.Show("System is null");
            //if(soundChannel.Channel == null) System.Windows.Forms.MessageBox.Show("New sound");

            if(soundChannel.Sound == null) return;

            // Wait for any fade on this channel to finish
            soundChannel.WaitOnFade();

            // Has it ever been run?
            Channel channel = null;
            if(soundChannel.Channel == null) {
                // Never run before
//System.Windows.Forms.MessageBox.Show("About to play the sound");
                ERRCHECK(m_system.playSound(FMOD.CHANNELINDEX.FREE, soundChannel.Sound, false, ref channel));
//System.Windows.Forms.MessageBox.Show("Playing the sound");
                soundChannel.Channel = channel;
            } else {
//System.Windows.Forms.MessageBox.Show("Reusing a channel");
                // Has been run before
                // Is the channel paused?
                if(soundChannel.Paused) {
                    // Paused, unpause it
                    soundChannel.Paused = false;
                } else {
                    // Not paused, channel was stopped
//System.Windows.Forms.MessageBox.Show("About to play the sound");
                    ERRCHECK(m_system.playSound(FMOD.CHANNELINDEX.REUSE, soundChannel.Sound, false, ref channel));
//System.Windows.Forms.MessageBox.Show("Playing the sound");
                    soundChannel.Channel = channel;
                }
            }
        }

        private void PlaySound(SoundChannel soundChannel, bool paused) {
            if(soundChannel == null || soundChannel.Sound == null) return;

            // Wait for any fade on this channel to finish
            soundChannel.WaitOnFade();

            // Has it ever been run?
            Channel channel = null;
            if(soundChannel.Channel == null) {
                // Never run before
                ERRCHECK(m_system.playSound(FMOD.CHANNELINDEX.FREE, soundChannel.Sound, paused, ref channel));
                soundChannel.Channel = channel;
            } else {
                // Has been run before
                // Is the channel paused?
                bool isPaused = false;
                soundChannel.Channel.getPaused(ref isPaused);
                if (isPaused) {
                    // Paused, unpause it
                    if (!paused) {
                        soundChannel.Channel.setPaused(false);
                    }
                } else {
                    // Not paused, channel was stopped
                    ERRCHECK(m_system.playSound(FMOD.CHANNELINDEX.REUSE, soundChannel.Sound, paused, ref channel));
                    soundChannel.Channel = channel;
                }
            }
        }

        public void Stop(SoundChannel soundChannel) {
            if(soundChannel != null && soundChannel.Channel != null) {
                soundChannel.Channel.stop();
                soundChannel.CancelFades();
            }
        }

        public void Stop(SoundChannel soundChannel, int fadeDuration) {
            if(soundChannel != null && soundChannel.Channel != null) {
                // Sound has to be playing for the volume to be set (fmod rule)
                if(fadeDuration != 0 && soundChannel.IsPlaying ) {
                    soundChannel.ImmediateFade(fadeDuration);
                    soundChannel.WaitOnFade();
                    soundChannel.Channel.stop();
                    soundChannel.CancelFades();
                }
            }
        }

        public void Shutdown() {
            foreach(SoundChannel soundChannel in m_channels) {
                soundChannel.Dispose();
            }

            if(m_system != null && --m_refCount == 0) {
                foreach(_System system in m_systems) {
                    ERRCHECK(system.release());
                }
                m_systems.Clear();
            }
        }

        public _System SystemObject {
            get { return m_system; }
        }

        private void ERRCHECK(FMOD.RESULT result) {
            if (result != FMOD.RESULT.OK) {
                throw new Exception( string.Format("Sound system error ({0})\n\n{1}",result, FMOD.Error.String(result)) );
            }
        }

    }

    #region SoundChannel
    public class SoundChannel : IDisposable {
        private const int FADE_TIMER_INTERVAL = 100;

        private Sound m_sound = null;
        private Channel m_channel = null;

        private uint m_soundLength = 0; // in ms
        private string m_soundName = string.Empty;
        private float m_channelVolume = 1.0f;
        private System.Timers.Timer m_fadeTimer;

        private enum FadeTimerState { Inactive, Entry, Threshold, Exit };
        private FadeTimerState m_fadeTimerState = FadeTimerState.Inactive;

        private float m_entryFadeDelta = 0;
        private float m_exitFadeDelta = 0;
        private float m_exitFadeThreshold = 0;

        private float m_normalFrequency;

        public SoundChannel(Sound sound) {
            m_fadeTimer = new System.Timers.Timer(FADE_TIMER_INTERVAL);
            m_fadeTimer.Elapsed += new ElapsedEventHandler(fadeTimer_Elapsed);
            this.Sound = sound;
        }

        public Sound Sound {
            get { return m_sound; }
            set {
                m_sound = value;
                m_sound.getLength(ref m_soundLength, TIMEUNIT.MS);
                m_soundName = GetSoundName(m_sound);
            }
        }

        public Channel Channel {
            get { return m_channel; }
            set {
                m_fadeTimerState = FadeTimerState.Inactive;
                m_channel = value;
                if(m_entryFadeDelta != 0) {
                    Volume = 0;
                    m_fadeTimerState = FadeTimerState.Entry;
                    m_fadeTimer.Enabled = true;
                }
                // If there's an exit fade, the fade timer needs to be started in order to
                // watch for the threshold.
                if(m_exitFadeDelta != 0 && !m_fadeTimer.Enabled) {
                    m_fadeTimerState = FadeTimerState.Threshold;
                    m_fadeTimer.Enabled = true;
                }
                m_channel.getFrequency(ref m_normalFrequency);
            }
        }

        public uint SoundLength {
            get { return m_soundLength; }
        }

        public string SoundName {
            get { return m_soundName; }
        }

        public float Volume {
            get {
                if(m_channel != null) {
                    m_channel.getVolume(ref m_channelVolume);
                    return m_channelVolume;
                }
                return 0;
            }
            set {
                if(m_channel != null) {
                    m_channel.setVolume(value);
                    m_channelVolume = value;
                }
            }
        }

        public float Frequency {
            // Use 0.0 - 1.0 as 0-100% of normal
            get {
                if(m_channel == null) return 0;
                float value = 0;
                m_channel.getFrequency(ref value);
                return value;
            }
            set {
                if(m_channel != null) {
                    m_channel.setFrequency(value * m_normalFrequency);
                }
            }
        }

        public bool Paused {
            get {
                if(m_channel != null) {
                    bool isPaused = false;
                    m_channel.getPaused(ref isPaused);
                    return isPaused;
                }
                return false;
            }
            set {
                if(m_channel != null) {
                    m_channel.setPaused(value);
                    if(m_fadeTimerState != FadeTimerState.Inactive) {
                        m_fadeTimer.Enabled = !value;
                    }
                }
            }
        }

        void fadeTimer_Elapsed(object sender, ElapsedEventArgs e) {
            switch(m_fadeTimerState) {
                case FadeTimerState.Inactive:
                    // It shouldn't be running, so kill it
                    m_fadeTimer.Enabled = false;
                    break;
                case FadeTimerState.Entry:
                    if(m_channelVolume < 1) {
                        // Increase the volume
                        if(1.0f - m_channelVolume < m_entryFadeDelta) {
                            // Will fmod cap it at 1 if a higher value is set, or does
                            // this need to be done to get it to set it when the value
                            // would be > 1?
                            Volume = 1;
                        } else {
                            Volume = m_channelVolume + m_entryFadeDelta;
                        }
                    } else if(m_exitFadeDelta != 0) {
                        // Done with the entry fade, but there's an exit fade that needs
                        // to be watched for.
                        m_fadeTimerState = FadeTimerState.Threshold;
                    } else {
                        // Done with the entry fade and there is no exit fade, so the fade
                        // timer is no longer needed.
                        m_fadeTimerState = FadeTimerState.Inactive;
                    }
                    break;
                case FadeTimerState.Threshold:
                    // Waiting to pass the exit fade threshold
                    if(m_exitFadeThreshold != 0 && Position >= m_exitFadeThreshold) {
                        // Start the exit fade
                        m_fadeTimerState = FadeTimerState.Exit;
                    }
                    break;
                case FadeTimerState.Exit:
                    if(m_channelVolume > 0) {
                        // Decrease the volume
                        if(m_channelVolume < -m_exitFadeDelta) {
                            // Will fmod cap it at 0 if a lower value is set, or does
                            // this need to be done to get it to set it when the value
                            // would be < 0?
                            Volume = 0;
                        } else {
                            Volume = m_channelVolume + m_exitFadeDelta;
                        }
                    } else {
                        // Done with the exit fade so the fade
                        // timer is no longer needed.
                        m_fadeTimerState = FadeTimerState.Inactive;
                    }
                    break;
            }
        }

        private string GetSoundName(Sound sound) {
            StringBuilder sb = new StringBuilder(256);
            sound.getName(sb, sb.Capacity);
            return sb.ToString().Trim();
        }

        public uint Position {
            get {
                // In milliseconds
                uint value = 0;
                if(m_channel != null) {
                    m_channel.getPosition(ref value, TIMEUNIT.MS);
                }
                return value;
            }

            set {
                if(m_channel != null) {
                    m_channel.setPosition(value, TIMEUNIT.MS);
                }
            }
        }

        public bool IsPlaying {
            get {
                if(m_channel == null || m_sound == null) return false;
                bool value = false;
                // It's considered to be playing if it's paused
                m_channel.isPlaying(ref value);
                return value;
            }
        }

        public void SetEntryFade(int durationInSeconds) {
            // This assumes 0-100 fade in
            if(durationInSeconds != 0) {
                m_entryFadeDelta = 1.0f / (durationInSeconds * ((float)1000 / FADE_TIMER_INTERVAL));
            } else {
                m_entryFadeDelta = 0;
            }
        }

        public void SetExitFade(int durationInSeconds) {
            // This assumes 100-0 fade out
            if(durationInSeconds != 0) {
                m_exitFadeDelta = -1.0f / (durationInSeconds * ((float)1000 / FADE_TIMER_INTERVAL));
                m_exitFadeThreshold = m_soundLength - durationInSeconds * 1000;
            } else {
                m_exitFadeDelta = 0;
                m_exitFadeThreshold = 0;
            }
        }

        public void ImmediateFade(int durationInSeconds) {
            if(durationInSeconds != 0) {
                m_exitFadeDelta = -1.0f / (durationInSeconds * ((float)1000 / FADE_TIMER_INTERVAL));
                m_exitFadeThreshold = Position + FADE_TIMER_INTERVAL;
                m_fadeTimerState = FadeTimerState.Threshold;
                if(!m_fadeTimer.Enabled) m_fadeTimer.Enabled = true;
            }
        }

        public void WaitOnFade() {
            // Side effect: This will wait for an exit fade to complete no matter where
            // the current position is in the audio.
            while(m_fadeTimer.Enabled) {
                Thread.Sleep(FADE_TIMER_INTERVAL);
            }
        }

        public void CancelFades() {
            m_fadeTimer.Enabled = false;
            m_fadeTimerState = FadeTimerState.Inactive;
            m_entryFadeDelta = 0;
            m_exitFadeDelta = 0;
            m_exitFadeThreshold = 0;
        }

        #region IDisposable Members

        public void Dispose() {
            if(m_fadeTimer.Enabled) {
                m_fadeTimer.Enabled = false;
            }
            bool playing = false;
            if(m_channel != null) {
                m_channel.isPlaying(ref playing);
                if(playing) {
                    m_channel.stop();
                }
                m_channel = null;
            }
            if(m_sound != null) {
                m_sound.release();
                m_sound = null;
            }
            GC.SuppressFinalize(this);
        }

        #endregion

        ~SoundChannel() {
            Dispose();
        }
    }
    #endregion


}
