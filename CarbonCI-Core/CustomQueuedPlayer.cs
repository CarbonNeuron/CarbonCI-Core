using System;
using System.IO;
using System.Threading.Tasks;
using Lavalink4NET.Decoding;
using Lavalink4NET.Events;
using Lavalink4NET.Player;
using Newtonsoft.Json;

namespace CarbonCI
{
    /// <summary>
    ///     A lavalink player with a queuing system.
    /// </summary>
    public class QueuedLavalinkPlayerV2 : LavalinkPlayer
    {
        private readonly bool _disconnectOnStop;
        private bool noSkip;
        private bool playerstate;

        /// <summary>
        ///     Initializes a new instance of the <see cref="QueuedLavalinkPlayerV2"/> class.
        /// </summary>
        public QueuedLavalinkPlayerV2()
        {
            Queue = new LavalinkQueue();
            DisconnectOnStop = false;
            // use own disconnect on stop logic
            _disconnectOnStop = DisconnectOnStop;
            
        }

        /// <summary>
        ///     Gets or sets a value indicating whether the current playing track should be looped.
        /// </summary>
        public bool IsLooping { get; set; }

        
        public string botResponse { get; set; }
        /// <summary>
        ///     Gets the track queue.
        /// </summary>
        public LavalinkQueue Queue { get; }

        /// <summary>
        ///     Asynchronously triggered when a track ends.
        /// </summary>
        /// <param name="eventArgs">the track event arguments</param>
        /// <returns>a task that represents the asynchronous operation</returns>
        public override async Task OnTrackEndAsync(TrackEndEventArgs eventArgs)
        {
            if (eventArgs.Reason == TrackEndReason.LoadFailed)
            {
                botResponse = "Load failed for song. Retrying.";
                Queue.Insert(0,TrackDecoder.DecodeTrack(eventArgs.TrackIdentifier));
                await SkipAsync();
                return;
            }
            Console.WriteLine($"Track end ran. Args: {eventArgs.Reason}");
            if (eventArgs.Reason != TrackEndReason.Replaced && Queue.IsEmpty)
            {
                DisconnectOnStop = false;
                await base.OnTrackEndAsync(eventArgs);
            }
            if (eventArgs.MayStartNext)
            {
                await SkipAsync();
            }



        }

        public override Task SetVolumeAsync(float volume = 1, bool normalize = false)
        {
            using (StreamWriter file = File.CreateText("volume.json"))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, volume*100);
            }
            return base.SetVolumeAsync(volume, normalize);
        }

        /// <summary>
        ///     Plays the specified <paramref name="track"/> asynchronously.
        /// </summary>
        /// <param name="track">the track to play</param>
        /// <param name="startTime">the track start position</param>
        /// <param name="endTime">the track end position</param>
        /// <param name="noReplace">
        ///     a value indicating whether the track play should be ignored if the same track is
        ///     currently playing
        /// </param>
        /// <returns>
        ///     a task that represents the asynchronous operation
        ///     <para>the position in the track queue ( <c>0</c> = now playing)</para>
        /// </returns>
        public new virtual Task<int> PlayAsync(LavalinkTrack track, TimeSpan? startTime = null,
            TimeSpan? endTime = null, bool noReplace = false)
            => PlayAsync(track, true, startTime, endTime, noReplace);

        /// <summary>
        ///     Plays the specified <paramref name="track"/> asynchronously.
        /// </summary>
        /// <param name="track">the track to play</param>
        /// <param name="enqueue">
        ///     a value indicating whether the track should be enqueued in the track queue
        /// </param>
        /// <param name="startTime">the track start position</param>
        /// <param name="endTime">the track end position</param>
        /// <param name="noReplace">
        ///     a value indicating whether the track play should be ignored if the same track is
        ///     currently playing
        /// </param>
        /// <returns>
        ///     a task that represents the asynchronous operation
        ///     <para>the position in the track queue ( <c>0</c> = now playing)</para>
        /// </returns>
        /// <exception cref="InvalidOperationException">thrown if the player is destroyed</exception>
        public virtual async Task<int> PlayAsync(LavalinkTrack track, bool enqueue,
            TimeSpan? startTime = null, TimeSpan? endTime = null, bool noReplace = false)
        {
            EnsureNotDestroyed();
            EnsureConnected();
            Console.WriteLine($"Enqueue: {enqueue}, State: {State}, {enqueue && State == PlayerState.Playing}");

            // check if the track should be enqueued (if a track is already playing)
            if (enqueue && State == PlayerState.Playing)
            {
                // add the track to the queue
                Queue.Add(track);
                botResponse = $"Adding {track.Title} to queue ({Queue.Count})";

                // return track queue position
                return Queue.Count;
            }
            await base.PlayAsync(track, startTime, endTime, noReplace);
            botResponse = "Playing " + track.Title;
            return 0;

        }

        /// <summary>
        ///     Plays the specified <paramref name="track"/> at the top of the queue asynchronously.
        /// </summary>
        /// <param name="track">the track to play</param>
        /// <returns>a task that represents the asynchronous operation</returns>
        /// <exception cref="InvalidOperationException">thrown if the player is destroyed</exception>
        public virtual async Task PlayTopAsync(LavalinkTrack track)
        {
            EnsureNotDestroyed();

            if (track is null)
            {
                throw new ArgumentNullException(nameof(track));
            }

            // play track if none is playing
            if (State == PlayerState.NotPlaying)
            {
                await PlayAsync(track, enqueue: false);
            }
            // the player is currently playing a track, enqueue the track at top
            else
            {
                Queue.Insert(0, track);
            }
        }

        /// <summary>
        ///     Pushes a track between the current asynchronously.
        /// </summary>
        /// <param name="track">the track to push between the current</param>
        /// <param name="push">
        ///     a value indicating whether the track should only played when a track is playing currently.
        /// </param>
        /// <remarks>
        ///     Note: This feature is experimental. This will stop playing the current track and
        ///     start playing the specified <paramref name="track"/> after the track is finished the
        ///     track will restart at the stopped position. This can be useful for example
        ///     soundboards (playing an air-horn or something).
        /// </remarks>
        /// <returns>
        ///     a task that represents the asynchronous operation. The task result is a value
        ///     indicating whether the track was pushed between the current ( <see
        ///     langword="true"/>) or the specified track was simply started ( <see
        ///     langword="false"/>), because there is no track playing.
        /// </returns>
        public virtual async Task<bool> PushTrackAsync(LavalinkTrack track, bool push = false)
        {
            Console.WriteLine(State.ToString());
            // star track immediately
            if (State == PlayerState.NotPlaying)
            {
                if (push)
                {
                    return false;
                }

                await PlayAsync(track, enqueue: false);
                return false;
            }

            // create clone and set starting position
            var oldTrack = CurrentTrack.WithPosition(TrackPosition);
            this.noSkip = true;
            await PlayAsync(track, enqueue: false);
            // enqueue old track with starting position
            await PlayTopAsync(oldTrack);
            return true;
        }

        /// <summary>
        ///     Skips the current track asynchronously.
        /// </summary>
        /// <param name="count">the number of tracks to skip</param>
        /// <returns>a task that represents the asynchronous operation</returns>
        /// <exception cref="InvalidOperationException">thrown if the player is destroyed</exception>
        public virtual Task SkipAsync(int count = 1)
        {
            // no tracks to skip
            if (count <= 0)
            {
                return Task.CompletedTask;
            }

            EnsureNotDestroyed();
            EnsureConnected();

            // the looping option is enabled, repeat current track, does not matter how often we skip
            if (IsLooping && CurrentTrack != null)
            {
                return PlayAsync(CurrentTrack, false);
            }
            // tracks are enqueued
            else if (!Queue.IsEmpty)
            {
                LavalinkTrack track = null;

                while (count-- > 0)
                {
                    // no more tracks in queue
                    if (Queue.IsEmpty)
                    {
                        // no tracks found
                        return Task.CompletedTask;
                    }

                    // dequeue track
                    track = Queue.Dequeue();
                }
                
                
                // a track to play was found, dequeue and play
                return PlayAsync(track, false);
            }
            else if (Queue.IsEmpty)
            {
                this.StopAsync(false);
            }
            // no tracks queued, disconnect if wanted

            return Task.CompletedTask;
        }

        /// <summary>
        ///     Stops playing the current track asynchronously.
        /// </summary>
        /// <param name="disconnect">
        ///     a value indicating whether the connection to the voice server should be closed
        /// </param>
        /// <returns>a task that represents the asynchronous operation</returns>
        /// <exception cref="InvalidOperationException">thrown if the player is destroyed</exception>
        public override Task StopAsync(bool disconnect = false)
        {
            Queue.Clear();
            return base.StopAsync(disconnect);
        }
    }
}