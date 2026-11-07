using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SadeSes
{
    public partial class MainForm
    {
        void Send(params object[] args) { try { player.Send(args); } catch { SetStatus("Ses motoruyla bağlantı kesildi. Parçayı yeniden açmayı dene.", true); } }
        void AddSelected(bool next = false)
        {
            if (view == LibraryView.Playlists) { CreatePlaylistDialog(null); return; }
            var track = Selected;
            if (track == null) return;
            int index = next && pendingQueueTrack != null ? queue.IndexOf(pendingQueueTrack) + 1 : 0;
            var queued = track.Copy();
            queue.Insert(next ? index : queue.Count, queued);
            if (pendingCollection != null) pendingCollection.Insert(next ? 0 : pendingCollection.Count, queued);
            SaveState(); RefreshQueueInfo();
            SetStatus((next ? "Sırada sonraki: " : "Sıranın sonuna eklendi: ") + track.Title, false);
        }
        void RefreshQueueInfo()
        {
            if (queueView) RefreshList();
            else
            {
                queueButton.AccessibleName = "Çalma sırası, " + queue.Count + " parça";
                tips.SetToolTip(queueButton, "Sırada " + queue.Count + " parça");
                ShowDetail();
            }
        }
        void RemoveSelected()
        {
            int index = list.SelectedIndex;
            if (index < 0) return;
            if (!queueView) { RemoveSavedTrack(); return; }
            Track track = queue[index];
            skipped.Remove(track);
            if (pendingCollection != null) pendingCollection.Remove(track);
            queue.RemoveAt(index); RefreshList(); SaveState();
        }
        async Task PlaySelected(bool collection = false)
        {
            if (view == LibraryView.Playlists) { OpenSelectedPlaylist(); return; }
            var track = Selected; if (track == null || preview || updating) return;
            var rest = collection ? CollectionAfter(list.SelectedIndex) : null;
            await Play(track, true, queueView ? track : null, rest);
        }
        async Task Play(Track track, bool remember, Track queueTrack = null, List<Track> collection = null, double start = 0, bool recovering = false)
        {
            if (closing || updating || preview) return;
            if (playCancel != null) { playCancel.Cancel(); playCancel.Dispose(); }
            playCancel = new CancellationTokenSource(); var token = playCancel.Token; int version = ++playVersion;
            if (remember && playable && current != null) history.Push(current);
            if (!recovering) recoveryAttempts = 0;
            current = track; pendingQueueTrack = queueTrack; pendingCollection = collection;
            loading = true; playable = false; paused = false;
            attemptStart = start;
            ShowCurrent(start); updateButton.Enabled = false;
            PauseIcon(false); pauseButton.Enabled = false;
            nowChannel.Text = recovering ? "Yeniden bağlanıyor…" : "Açılıyor…";
            ShowDetail(); Task ignored = EnsureCover(track);
            SetStatus(recovering ? "Ses bağlantısı yenileniyor…" : "Ses bağlantısı açılıyor…", false);
            if (!recovering) Send("stop");
            SaveState();
            Task recovery = null;
            try
            {
                if (recovering)
                {
                    await Task.Delay(750, token);
                    if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                    {
                        SetStatus("Ağ bağlantısı bekleniyor…", false);
                        while (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                            await Task.Delay(1000, token);
                    }
                }
                await player.Start(); if (version != playVersion || closing) return;
                var data = await Tools.Resolve(track, token, recoveryAttempts); if (version != playVersion || closing) return;
                player.Load(data, volumeLevel, version, position);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (version == playVersion && !closing)
                    recovery = RecoverPlayback(ex.Message, ex is SourceException ? ((SourceException)ex).Transient : ex is IOException);
            }
            if (recovery != null) await recovery;
        }
        void ShowCurrent(double start)
        {
            position = start; duration = Math.Max(duration, start);
            if (current != null) duration = Math.Max(current.Duration, start);
            seek.Maximum = Math.Max(1, duration); seek.UpdateValue(position);
            elapsed.Text = Tools.Time(position); total.Text = duration > 0 ? Tools.Time(duration) : "—";
            if (current != null) { nowTitle.Text = current.Title; nowChannel.Text = "Duraklatıldı · " + current.Channel; }
            UpdateMiniCover();
        }
        void PauseIcon(bool isPlaying, bool retry = false)
        {
            string label = isPlaying ? "Duraklat" : retry ? "Tekrar dene" : "Oynat / devam et";
            pauseButton.Glyph = isPlaying ? "pause" : "play"; pauseButton.AccessibleName = label; tips.SetToolTip(pauseButton, label); pauseButton.Invalidate();
        }
        void SetRepeat(bool enabled)
        {
            repeating = enabled; repeatButton.Active = enabled;
            repeatButton.AccessibleName = "Parçayı tekrarla: " + (enabled ? "açık" : "kapalı"); repeatButton.Invalidate();
        }
        async Task TogglePlayback()
        {
            if (loading || updating) return;
            if (current == null) { if (Selected != null) await PlaySelected(); else if (queue.Count > 0) await Next(); return; }
            if (playable) Send("cycle", "pause");
            else await Play(current, false, pendingQueueTrack, pendingCollection, position);
        }
        async Task RecoverPlayback(string error, bool transient)
        {
            if (current == null) return;
            if (transient && !current.IsLocal && recoveryAttempts < 2)
            {
                recoveryAttempts++;
                Tools.ForgetStream(current.Id);
                await Play(current, false, pendingQueueTrack, pendingCollection, position, true);
                return;
            }
            if (!current.IsLocal) Tools.ForgetStream(current.Id);
            loading = false; playable = false; paused = true; pauseButton.Enabled = true;
            nowChannel.Text = "Parça açılamadı"; PauseIcon(false, true); updateButton.Enabled = !searching;
            SetStatus(error + " Tekrar dene veya sonraki parçaya geç.", true); SaveState();
        }
        Track NextQueueTrack() { return queue.FirstOrDefault(item => !ReferenceEquals(item, pendingQueueTrack) && !skipped.Contains(item)); }
        async Task Next()
        {
            if (updating) return;
            var track = NextQueueTrack();
            if (track == null) { SetStatus("Sırada başka parça yok. Sevdiğin parçaları sıraya ekle.", false); return; }
            if (pendingQueueTrack != null && queue.Contains(pendingQueueTrack)) skipped.Add(pendingQueueTrack);
            await Play(track, true, track);
        }
        async Task Previous() { if (history.Count > 0) await Play(history.Pop(), false); else if (current != null && !loading) Send("seek", 0, "absolute"); }
        void Stop()
        {
            if (playCancel != null) playCancel.Cancel(); playVersion++; loading = false; playable = false; current = pendingQueueTrack = null; pendingCollection = null; position = duration = 0; Send("stop"); paused = false; pauseButton.Enabled = true; PauseIcon(false); nowTitle.Text = "Dinlemeye hazırsın"; nowChannel.Text = "Bir parça seç, dinlemeye başla."; seek.UpdateValue(0); elapsed.Text = total.Text = "0:00"; updateButton.Enabled = !searching; UpdateMiniCover(); ShowDetail(); SetStatus("Çalma durduruldu.", false); SaveState();
        }
        void OnPlayerMessage(Dictionary<string, object> message)
        {
            Ui(async () => {
                if (current == null || Json.Num(message, "play_version") != playVersion) return;
                string ev = Json.Str(message, "event");
                if (ev == "property-change")
                {
                    string name = Json.Str(message, "name"); object value = Json.Get(message, "data"); if (value == null) return;
                    if (name == "time-pos" && playable)
                    {
                        position = Convert.ToDouble(value); seek.UpdateValue(position); elapsed.Text = Tools.Time(position);
                        if (position >= attemptStart + 10) recoveryAttempts = 0;
                    }
                    if (name == "duration") { duration = Convert.ToDouble(value); seek.Maximum = Math.Max(1, duration); total.Text = Tools.Time(duration); }
                    if (name == "pause" && playable) { paused = Convert.ToBoolean(value); PauseIcon(!paused); nowChannel.Text = (paused ? "Duraklatıldı · " : "") + current.Channel; if (paused) SaveState(); }
                    if (name == "paused-for-cache" && current != null && !loading) SetStatus(Convert.ToBoolean(value) ? "Ses yükleniyor…" : "Şimdi çalıyor: " + current.Title, false);
                }
                else if (ev == "file-loaded")
                {
                    loading = false; playable = true; pauseButton.Enabled = true;
                    CommitLoadedQueue();
                    recent.RemoveAll(t => t.Id == current.Id); recent.Insert(0, current);
                    if (recent.Count > 40) recent.RemoveAt(recent.Count - 1);
                    PauseIcon(true); nowChannel.Text = current.Channel; updateButton.Enabled = !searching;
                    SetStatus("Şimdi çalıyor: " + current.Title, false);
                    SaveState(); if (view == LibraryView.Recent) RefreshList(); else RefreshQueueInfo();
                }
                else if (ev == "engine-disconnected" || ev == "load-error")
                    await RecoverPlayback("Ses motoruyla bağlantı kurulamadı.", true);
                else if (ev == "end-file")
                {
                    string reason = Json.Str(message, "reason");
                    if (reason == "error") await RecoverPlayback(current.IsLocal ? "Yerel ses dosyası açılamadı." : "Ses bağlantısı açılamadı.", true);
                    if (reason == "eof" && !loading)
                    {
                        if (!current.IsLocal && duration > 0 && position > 0 && position + 3 < duration)
                            await RecoverPlayback("Ses akışı parça bitmeden kesildi.", true);
                        else if (repeating) await Play(current, false);
                        else if (NextQueueTrack() != null) await Next();
                        else { current = null; playable = false; position = 0; PauseIcon(false); nowChannel.Text = "Parça bitti"; SaveState(); SetStatus("Sıraya yeni parçalar ekleyebilirsin.", false); }
                    }
                }
            });
        }
        void CommitLoadedQueue()
        {
            if (pendingQueueTrack != null) { queue.Remove(pendingQueueTrack); skipped.Remove(pendingQueueTrack); }
            if (pendingCollection != null) { queue.Clear(); skipped.Clear(); queue.AddRange(pendingCollection); }
            pendingQueueTrack = null; pendingCollection = null;
        }
    }
}
