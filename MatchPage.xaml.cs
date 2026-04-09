using System.Collections.ObjectModel;
using System.Text.Json;
using Android.Provider;
using MyAndroidApp.Models;
using MyAndroidApp.Services;

namespace MyAndroidApp;

public partial class MatchPage : ContentPage
{
    public ObservableCollection<MatchupRound> MatchRounds { get; set; } = new ObservableCollection<MatchupRound>();
    private readonly MemberDatabase _database;
    private int _matchRoundCount = 0;
    private CancellationTokenSource _speechCancellation;
    private CancellationTokenSource _timerCancellation;
    private DateTime? _timerEndTime;
    private bool _isAlarming = false;

    public MatchPage(MemberDatabase database)
    {
        InitializeComponent();
        _database = database;
        MatchRoundsCollectionView.ItemsSource = MatchRounds;
        _database.OnEventReset += HandleEventReset;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (MatchRounds.Count == 0)
        {
            await LoadMatchDataAsync();
        }

        // バックグラウンド復帰時に残り時間を再表示し、必要ならアラームを開始
        if (_timerEndTime.HasValue)
        {
            var remaining = _timerEndTime.Value - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                MainThread.BeginInvokeOnMainThread(() => StartAlarm());
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() => TimerLabel.Text = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}");
            }
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _isAlarming = false;
        AlarmOverlay.IsVisible = false;
    }

    private void HandleEventReset()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            MatchRounds.Clear();
            _matchRoundCount = 0;
            SaveMatchData();
            StopCalling();
            StopTimer();
            _isAlarming = false;
            AlarmOverlay.IsVisible = false;
        });
    }

    private void StopCalling()
    {
        if (_speechCancellation != null)
        {
            _speechCancellation.Cancel();
            _speechCancellation.Dispose();
            _speechCancellation = null;
        }
    }

    private void StopTimer()
    {
        if (_timerCancellation != null)
        {
            _timerCancellation.Cancel();
            _timerCancellation.Dispose();
            _timerCancellation = null;
        }
        _timerEndTime = null;
        MainThread.BeginInvokeOnMainThread(() => {
            if (TimerLabel != null) TimerLabel.Text = "--:--";
        });
    }

    private void SaveMatchData()
    {
        var saveData = new MatchSaveData { CurrentRoundCount = _matchRoundCount };
        foreach (var round in MatchRounds)
        {
            var roundData = new RoundSaveData
            {
                RoundNumber = round.RoundNumber,
                BreakMemberIds = round.BreakMembers.Select(m => m.Id).ToList()
            };
            foreach (var court in round.Courts)
            {
                roundData.Courts.Add(new CourtSaveData
                {
                    MatchName = court.MatchName,
                    P1Id = court.Player1?.Id,
                    P2Id = court.Player2?.Id,
                    P3Id = court.Player3?.Id,
                    P4Id = court.Player4?.Id
                });
            }
            saveData.Rounds.Add(roundData);
        }
        string json = JsonSerializer.Serialize(saveData);
        Preferences.Default.Set("SavedMatchData", json);
    }

    private async Task LoadMatchDataAsync()
    {
        string json = Preferences.Default.Get("SavedMatchData", string.Empty);
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var saveData = JsonSerializer.Deserialize<MatchSaveData>(json);
            if (saveData == null) return;
            var allMembers = await _database.GetMembersAsync();
            _matchRoundCount = saveData.CurrentRoundCount;
            MatchRounds.Clear();
            foreach (var roundData in saveData.Rounds)
            {
                var round = new MatchupRound { RoundNumber = roundData.RoundNumber };
                foreach (var id in roundData.BreakMemberIds)
                {
                    var member = allMembers.FirstOrDefault(m => m.Id == id);
                    if (member != null) round.BreakMembers.Add(member);
                }
                foreach (var courtData in roundData.Courts)
                {
                    round.Courts.Add(new Matchup
                    {
                        ParentRound = round,
                        MatchName = courtData.MatchName,
                        Player1 = allMembers.FirstOrDefault(m => m.Id == courtData.P1Id),
                        Player2 = allMembers.FirstOrDefault(m => m.Id == courtData.P2Id),
                        Player3 = allMembers.FirstOrDefault(m => m.Id == courtData.P3Id),
                        Player4 = allMembers.FirstOrDefault(m => m.Id == courtData.P4Id)
                    });
                }
                MatchRounds.Add(round);
            }
        }
        catch { }
    }

    private async void OnNextMatchClicked(object sender, EventArgs e)
    {
        StopCalling();
        StopTimer();
        _isAlarming = false;
        AlarmOverlay.IsVisible = false;

        var allMembers = await _database.GetMembersAsync();
        var participatingMembers = allMembers.Where(m => m.IsParticipating).ToList();

        if (participatingMembers.Count < 4)
        {
            await DisplayAlert("エラー", "参加者が4人未満のため、試合を組めません。", "OK");
            return;
        }

        int courtCount = Preferences.Default.Get("CourtCount", 2);
        string settingsJson = Preferences.Default.Get("MatchupSettings", string.Empty);
        var settingsItems = string.IsNullOrEmpty(settingsJson) 
            ? new List<MatchSettingItem>() 
            : JsonSerializer.Deserialize<List<MatchSettingItem>>(settingsJson);

        int requiredPlayers = courtCount * 4;
        int playersToSelect = (Math.Min(requiredPlayers, participatingMembers.Count) / 4) * 4;
        int matchesToCreate = playersToSelect / 4;

        if (matchesToCreate == 0)
        {
            await DisplayAlert("エラー", "参加者が4人未満のため、試合を組めません。", "OK");
            return;
        }

        var previousBreakIds = MatchRounds.Count > 0 
            ? MatchRounds[0].BreakMembers.Select(m => m.Id).ToHashSet() 
            : new HashSet<string>();

        var random = new Random();

        var selectedPlayers = participatingMembers
            .OrderBy(m => previousBreakIds.Contains(m.Id) ? 0 : 1)
            .ThenBy(m => m.MatchRate)
            .ThenBy(m => random.Next())
            .Take(playersToSelect)
            .ToList();
        
        bool prioritizeGender = settingsItems?.FirstOrDefault(s => s.Key == "PrioritizeGender")?.IsEnabled ?? false;
        bool prioritizeMixedDoubles = settingsItems?.FirstOrDefault(s => s.Key == "PrioritizeMixedDoubles")?.IsEnabled ?? false;
        bool avoidFemale = settingsItems?.FirstOrDefault(s => s.Key == "AvoidSingleFemale")?.IsEnabled ?? false;
        bool avoidMale = settingsItems?.FirstOrDefault(s => s.Key == "AvoidSingleMale")?.IsEnabled ?? false;

        if(participatingMembers.Count() != playersToSelect && (prioritizeGender || avoidFemale || avoidMale))
        {
            int maxPoint = int.MinValue;
            var maxMatchRate = selectedPlayers.Max(m => m.MatchRate);
            // 半分ランダムに1000組み合わせを作成して採点する
            for(int i = 0; i < 1000; i++)
            {
                var rndList = participatingMembers.OrderBy(m => previousBreakIds.Contains(m.Id) ? 0 : 1).ThenBy(m => m.MatchRate).ThenBy(m => random.Next()).Take(playersToSelect / 2).ToList();
                rndList.AddRange(participatingMembers.Except(rndList).OrderBy(m => random.Next()).Take(playersToSelect / 2).ToList());
                
                var breakList = participatingMembers.Except(rndList).ToList();
                int point = 0;
                // 2連続休憩の場合は減点
                point -= breakList.Count(m => previousBreakIds.Contains(m.Id)) * 10000;
                // レートの最大と最小が逆だと減点
                if (rndList.Max(m => m.MatchRate) > participatingMembers.Except(rndList).Min(m => m.MatchRate)) point -= 5000;

                // 性別が偶数の場合は加点
                if (prioritizeGender && rndList.Count(m => m.Gender == Gender.男性) % 4 == 0) point += 1000;
                // ミックス有線の場合男女同数は最高
                if (prioritizeMixedDoubles && rndList.Count(m => m.Gender == Gender.男性) == rndList.Count(m => m.Gender == Gender.女性)) point += 1000;
                if ((avoidFemale || avoidMale) && rndList.Count(m => m.Gender == Gender.男性) % 2 == 0) point += 200;

                if(maxPoint < point)
                {
                    maxPoint = point;
                    selectedPlayers = rndList;
                }
            }
        }

        //if (avoidFemale || avoidMale)
        //{
        //    // 選出された全メンバー中、女性が奇数人いる場合（＝必ずどこかのコートで奇数になる）
        //    int fCount = selectedPlayers.Count(p => p.Gender == Gender.女性);
        //    if (fCount % 2 != 0)
        //    {
        //        // 他に待機中の女性がいるなら1人呼んでくる（奇数+1 = 偶数）
        //        var anotherFemale = participatingMembers.Except(selectedPlayers).OrderBy(m => m.MatchCount).FirstOrDefault(p => p.Gender == Gender.女性);
        //        if (anotherFemale != null)
        //        {
        //            var maleToOut = selectedPlayers.Where(p => p.Gender == Gender.男性).OrderByDescending(p => p.MatchCount).FirstOrDefault();
        //            if (maleToOut != null) { selectedPlayers.Remove(maleToOut); selectedPlayers.Add(anotherFemale); }
        //        }
        //        else
        //        {
        //            // 待機中に女性がいないなら、選出中の女性を1人休ませる（奇数-1 = 偶数）
        //            var femaleToOut = selectedPlayers.Where(p => p.Gender == Gender.女性).OrderByDescending(p => p.MatchCount).FirstOrDefault();
        //            var anotherMale = participatingMembers.Except(selectedPlayers).OrderBy(m => m.MatchCount).FirstOrDefault(p => p.Gender == Gender.男性);
        //            if (femaleToOut != null && anotherMale != null) { selectedPlayers.Remove(femaleToOut); selectedPlayers.Add(anotherMale); }
        //        }
        //    }
        //}

        var breakMembers = participatingMembers.Except(selectedPlayers).ToList();

        if (MatchRounds.Count >= 2)
        {
            var twoRoundsAgoPlayerIds = MatchRounds[1].Courts
                .SelectMany(c => new[] { c.Player1?.Id, c.Player2?.Id, c.Player3?.Id, c.Player4?.Id })
                .ToHashSet();

            if (selectedPlayers.Count == twoRoundsAgoPlayerIds.Count &&
                selectedPlayers.All(p => twoRoundsAgoPlayerIds.Contains(p.Id)))
            {
                if (breakMembers.Any())
                {
                    var pToOut = selectedPlayers
                        .OrderByDescending(m =>
                        {
                            int total = m.MatchCount + m.BreakCount;
                            return total == 0 ? 0 : (double)m.MatchCount / total;
                        })
                        .ThenBy(m => random.Next())
                        .First();

                    var pToIn = breakMembers
                        .OrderBy(m =>
                        {
                            int total = m.MatchCount + m.BreakCount;
                            return total == 0 ? 0 : (double)m.MatchCount / total;
                        })
                        .ThenBy(m => random.Next())
                        .First();

                    selectedPlayers.Remove(pToOut);
                    selectedPlayers.Add(pToIn);
                    breakMembers = participatingMembers.Except(selectedPlayers).ToList();
                }
            }
        }

        var bestGrouping = OptimizePairings(selectedPlayers, matchesToCreate, settingsItems);

        _matchRoundCount++;
        var newRound = new MatchupRound
        {
            RoundNumber = _matchRoundCount,
            BreakMembers = new ObservableCollection<Member>(breakMembers)
        };

        foreach (var bm in breakMembers) { bm.BreakCount++; await _database.SaveMemberAsync(bm); }

        for (int i = 0; i < matchesToCreate; i++)
        {
            var courtPlayers = bestGrouping[i];
            foreach (var p in courtPlayers) { p.MatchCount++; await _database.SaveMemberAsync(p); }

            newRound.Courts.Add(new Matchup
            {
                ParentRound = newRound,
                MatchName = $"第 {i + 1} コート",
                Player1 = courtPlayers[0],
                Player2 = courtPlayers[1],
                Player3 = courtPlayers[2],
                Player4 = courtPlayers[3]
            });
        }

        MatchRounds.Insert(0, newRound);
        await Task.Delay(150);

        if (MatchRounds.Count > 0)
        {
            MatchRoundsCollectionView.ScrollTo(0, position: ScrollToPosition.Start, animate: true);
        }

        SaveMatchData();
        
        // 読み上げとタイマーの開始
        _ = RunMatchSequenceAsync(newRound);
    }

    private async Task RunMatchSequenceAsync(MatchupRound round)
    {
        // 1. 読み上げを実行
        await CallMatchesAsync(round);

        // 2. タイマー開始（読み上げが中断されなかった場合）
        StartMatchTimer();
    }

    private void StartMatchTimer()
    {
        StopTimer();

        try
        {
            string timeStr = Preferences.Default.Get("MatchTime", "無制限");
            if (string.IsNullOrEmpty(timeStr) || timeStr == "無制限") return;

            var match = System.Text.RegularExpressions.Regex.Match(timeStr, @"\d+");
            if (!match.Success) return;

            int minutes = int.Parse(match.Value);
            int totalSeconds = minutes * 60;
            _timerEndTime = DateTime.UtcNow.AddSeconds(totalSeconds);

            _timerCancellation = new CancellationTokenSource();
            var token = _timerCancellation.Token;

            Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        var remaining = _timerEndTime.Value - DateTime.UtcNow;
                        if (remaining <= TimeSpan.Zero)
                        {
                            MainThread.BeginInvokeOnMainThread(() => TimerLabel.Text = "00:00");
                            MainThread.BeginInvokeOnMainThread(StartAlarm);
                            break;
                        }

                        int m = remaining.Minutes;
                        int s = remaining.Seconds;
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            if (TimerLabel != null) TimerLabel.Text = $"{m:D2}:{s:D2}";
                        });

                        var delay = TimeSpan.FromMilliseconds(250);
                        await Task.Delay(delay, token);
                    }
                }
                catch (OperationCanceledException) { }
            }, token);
        }
        catch { }
    }

    private async void StartAlarm()
    {
        if (_isAlarming)
            return;

        try
        {
            _isAlarming = true;
            if (AlarmOverlay != null) AlarmOverlay.IsVisible = true;
            var options = new SpeechOptions() { Volume = 1.0f, Pitch = 1.0f };

            // アラーム停止まで音声を繰り返す
            while (_isAlarming)
            {
                try { await TextToSpeech.Default.SpeakAsync("試合終了です。コートを空けてください。", options); }
                catch { }
                if (_isAlarming) await Task.Delay(1000);
            }
        }
        catch
        {
            _isAlarming = false;
        }
    }

    private void OnAlarmOverlayTapped(object sender, EventArgs e)
    {
        _isAlarming = false;
        AlarmOverlay.IsVisible = false;
    }

    private async void OnCancelMatchClicked(object sender, EventArgs e)
    {
        StopCalling();
        StopTimer();
        _isAlarming = false;
        AlarmOverlay.IsVisible = false;

        if (MatchRounds.Count == 0)
        {
            await DisplayAlert("エラー", "キャンセルする試合がありません。", "OK");
            return;
        }

        var latestRound = MatchRounds[0];
        bool confirm = await DisplayAlert("確認", $"第 {latestRound.RoundNumber} 回戦の組み合わせをすべてキャンセルし、回数を戻しますか？", "はい", "いいえ");
        if (confirm)
        {
            foreach (var match in latestRound.Courts)
            {
                await DecrementMatchCountAsync(match.Player1);
                await DecrementMatchCountAsync(match.Player2);
                await DecrementMatchCountAsync(match.Player3);
                await DecrementMatchCountAsync(match.Player4);
            }
            foreach (var bm in latestRound.BreakMembers)
            {
                await DecrementBreakCountAsync(bm);
            }
            MatchRounds.RemoveAt(0);
            if (_matchRoundCount > 0) _matchRoundCount--;
            SaveMatchData();
        }
    }

    private async Task CallMatchesAsync(MatchupRound round)
    {
        int callCount = Preferences.Default.Get("CallCount", 1);
        if (callCount <= 0) return;
        _speechCancellation = new CancellationTokenSource();
        var token = _speechCancellation.Token;
        var sb = new System.Text.StringBuilder();
        sb.Append("試合をコールします。");
        foreach (var court in round.Courts)
        {
            sb.Append($"{court.MatchName}。");
            sb.Append($"{GetMemberCallName(court.Player1)}、{GetMemberCallName(court.Player2)} 対 ");
            sb.Append($"{GetMemberCallName(court.Player3)}、{GetMemberCallName(court.Player4)}。");
        }
        string callText = sb.ToString();
        var options = new SpeechOptions() { Volume = 1.0f, Pitch = 0.9f };
        try
        {
            for (int i = 0; i < callCount; i++)
            {
                await TextToSpeech.Default.SpeakAsync(callText, options, cancelToken: token);
                if (i < callCount - 1) await Task.Delay(1000, token);
            }
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private string GetMemberCallName(Member member)
    {
        if (member == null) return "不明";
        return string.IsNullOrWhiteSpace(member.Reading) ? member.Name : member.Reading;
    }

    private async Task DecrementMatchCountAsync(Member player)
    {
        if (player == null) return;
        var members = await _database.GetMembersAsync();
        var dbMember = members.FirstOrDefault(m => m.Id == player.Id);
        if (dbMember != null && dbMember.MatchCount > 0)
        {
            dbMember.MatchCount--;
            await _database.SaveMemberAsync(dbMember);
        }
    }

    private async Task DecrementBreakCountAsync(Member player)
    {
        if (player == null) return;
        var members = await _database.GetMembersAsync();
        var dbMember = members.FirstOrDefault(m => m.Id == player.Id);
        if (dbMember != null && dbMember.BreakCount > 0)
        {
            dbMember.BreakCount--;
            await _database.SaveMemberAsync(dbMember);
        }
    }

    private List<List<Member>> OptimizePairings(List<Member> players, int matchCount, List<MatchSettingItem> settings)
    {
        var random = new Random();
        var latestRound = MatchRounds.Any() ? MatchRounds[0] : null;
        // 設定が空、または有効な設定が一つもない場合は、完全ランダムな組み合わせを1つ返して終了
        if (settings == null || !settings.Any(s => s.IsEnabled))
        {
            var shuffled = players.OrderBy(x => random.Next()).ToList();
            var randomGroup = new List<List<Member>>();
            for (int m = 0; m < matchCount; m++)
            {
                randomGroup.Add(shuffled.Skip(m * 4).Take(4).ToList());
            }
            return randomGroup;
        }

        List<List<Member>> bestGroup = null;
        double bestScore = double.MinValue;

        // 1000回シミュレーションして最適なスコアを探す
        for (int i = 0; i < 1000; i++)
        {
            var shuffled = players.OrderBy(x => random.Next()).ToList();
            var currentGroup = new List<List<Member>>();
            double currentTotalScore = 0;

            for (int m = 0; m < matchCount; m++)
            {
                var courtPlayers = shuffled.Skip(m * 4).Take(4).ToList();
                currentGroup.Add(courtPlayers);
                currentTotalScore += CalculateCourtScore(courtPlayers, settings);
            }
            // 前回と同じペアがある場合減点
            if(latestRound != null)
            {
                foreach (var item in currentGroup)
                {
                    if (latestRound.Courts.Any(court => court.pairs.Contains($"{item[0].Id}_{item[1].Id}"))) currentTotalScore--;
                    if (latestRound.Courts.Any(court => court.pairs.Contains($"{item[2].Id}_{item[3].Id}"))) currentTotalScore--;
                }
                
            }

            if (currentTotalScore > bestScore)
            {
                bestScore = currentTotalScore;
                bestGroup = currentGroup;
            }
        }
        return bestGroup ?? new List<List<Member>>();
    }

    private double CalculateCourtScore(List<Member> p, List<MatchSettingItem> settings)
    {
        if (p.Count < 4) return 0;
        double score = 0;

        int maleCount = p.Count(m => m.Gender == Gender.男性);
        int femaleCount = p.Count(m => m.Gender == Gender.女性);

        // ペアごとの性別構成をチェック (P1&P2, P3&P4)
        bool pair1IsSame = p[0].Gender == p[1].Gender;
        bool pair2IsSame = p[2].Gender == p[3].Gender;
        bool pair1IsMixed = p[0].Gender != p[1].Gender;
        bool pair2IsMixed = p[2].Gender != p[3].Gender;

        for (int i = 0; i < settings.Count; i++)
        {
            var s = settings[i];
            if (!s.IsEnabled) continue;

            // リストの上にあるほど影響力を強くする
            double weight = (settings.Count - i) * 20.0;
            switch (s.Key)
            {
                case "PrioritizeGender":
                    bool p1Male = p[0].Gender == Gender.男性 && p[1].Gender == Gender.男性;
                    bool p1Female = p[0].Gender == Gender.女性 && p[1].Gender == Gender.女性;
                    bool p2Male = p[2].Gender == Gender.男性 && p[3].Gender == Gender.男性;
                    bool p2Female = p[2].Gender == Gender.女性 && p[3].Gender == Gender.女性;

                    if (p1Male && p2Male) score += weight * 2;      // 男ダブ vs 男ダブ (最高評価)
                    else if (p1Female && p2Female) score += weight * 2; // 女ダブ vs 女ダブ (最高評価)
                    else if ((p1Male && p2Female) || (p1Female && p2Male)) score -= weight; // 男ダブ vs 女ダブ (回避対象)
                    else
                    {
                        // それ以外（片方だけ同性ペアなど）
                        if (pair1IsSame) score += weight;
                        if (pair2IsSame) score += weight;
                    }
                    break;
                case "PrioritizeMixed":
                    if (pair1IsMixed) score += weight;
                    if (pair2IsMixed) score += weight;
                    if (maleCount == 2 && femaleCount == 2 && pair1IsMixed && pair2IsMixed) score += weight;
                    break;
                case "AvoidSingleFemale":
                    if (femaleCount % 2 != 0) score -= 1000; // 1人または3人なら特大の減点
                    break;
                case "AvoidSingleMale":
                    if (maleCount % 2 != 0) score -= 1000; // 1人または3人なら特大の減点
                    break;
            }
        }
        return score;
    }

    private async Task PlayBounceAnimation(Frame frame)
    {
        if (frame == null) return;
        await frame.ScaleTo(1.15, 100, Easing.CubicOut);
        await frame.ScaleTo(1.0, 100, Easing.CubicIn);
    }

    private Frame _selectedFrame = null;
    private DragInfo _selectedInfo = null;

    private async void OnP1Tapped(object sender, TappedEventArgs e) => await HandleTapAsync(sender, 1);
    private async void OnP2Tapped(object sender, TappedEventArgs e) => await HandleTapAsync(sender, 2);
    private async void OnP3Tapped(object sender, TappedEventArgs e) => await HandleTapAsync(sender, 3);
    private async void OnP4Tapped(object sender, TappedEventArgs e) => await HandleTapAsync(sender, 4);

    private async void OnBreakTapped(object sender, TappedEventArgs e)
    {
        if (sender is Frame frame && frame.BindingContext is Member member)
        {
            var sourceRound = MatchRounds.FirstOrDefault(r => r.BreakMembers.Contains(member));
            if (sourceRound == null) return;
            var info = new DragInfo { Member = member, SourceRound = sourceRound, IsFromBreakArea = true };
            await ProcessSelectionAsync(frame, info, null, 0, member);
        }
    }

    private async Task HandleTapAsync(object sender, int playerIndex)
    {
        if (sender is Frame frame && frame.BindingContext is Matchup matchup)
        {
            var member = playerIndex switch { 1 => matchup.Player1, 2 => matchup.Player2, 3 => matchup.Player3, _ => matchup.Player4 };
            var info = new DragInfo { Member = member, SourceMatchup = matchup, PlayerIndex = playerIndex, IsFromBreakArea = false };
            await ProcessSelectionAsync(frame, info, matchup, playerIndex, member);
        }
    }

    private async Task ProcessSelectionAsync(Frame tappedFrame, DragInfo tappedInfo, Matchup targetMatchup, int targetPlayerIndex, Member targetMember)
    {
        if (_selectedInfo == null)
        {
            _selectedFrame = tappedFrame;
            _selectedInfo = tappedInfo;
            HighlightElement(_selectedFrame);
            return;
        }
        if (_selectedInfo.Member?.Id == targetMember?.Id)
        {
            ResetElement(_selectedFrame);
            _selectedInfo = null;
            _selectedFrame = null;
            return;
        }
        ResetElement(_selectedFrame);
        if (targetMatchup != null)
        {
            await SwapMembersAsync(_selectedInfo, targetMatchup, targetPlayerIndex, targetMember);
        }
        else
        {
            var targetRound = MatchRounds.FirstOrDefault(r => r.BreakMembers.Contains(targetMember));
            if (targetRound != null)
            {
                await SwapMembersWithBreakAsync(_selectedInfo, targetRound, targetMember);
            }
        }
        await Task.WhenAll(PlayBounceAnimation(_selectedFrame), PlayBounceAnimation(tappedFrame));
        _selectedInfo = null;
        _selectedFrame = null;
    }

    private void HighlightElement(Frame frame)
    {
        frame.BackgroundColor = Color.FromArgb("#2196F3");
        if (frame.Content is Label label) label.TextColor = Colors.White;
    }

    private void ResetElement(Frame frame)
    {
        if (frame == null) return;
        if (frame.BindingContext is Member member)
        {
            frame.BackgroundColor = Color.FromArgb("#E0E0E0");
            if (frame.Content is Label label) label.TextColor = Colors.Black;
        }
        else if (frame.BindingContext is Matchup matchup)
        {
            frame.ClearValue(Microsoft.Maui.Controls.Frame.BackgroundColorProperty);
            if (frame.Content is Label label) label.TextColor = Color.FromArgb("#333333");
        }
    }

    private async Task SwapMembersAsync(DragInfo dragInfo, Matchup targetMatchup, int targetPlayerIndex, Member targetMember)
    {
        if (!dragInfo.IsFromBreakArea)
        {
            SetPlayerToMatchup(dragInfo.SourceMatchup, dragInfo.PlayerIndex, targetMember);
            SetPlayerToMatchup(targetMatchup, targetPlayerIndex, dragInfo.Member);
        }
        else
        {
            dragInfo.Member.BreakCount--;
            dragInfo.Member.MatchCount++;
            await _database.SaveMemberAsync(dragInfo.Member);
            targetMember.MatchCount--;
            targetMember.BreakCount++;
            await _database.SaveMemberAsync(targetMember);
            int breakIndex = dragInfo.SourceRound.BreakMembers.IndexOf(dragInfo.Member);
            if (breakIndex >= 0) dragInfo.SourceRound.BreakMembers[breakIndex] = targetMember;
            SetPlayerToMatchup(targetMatchup, targetPlayerIndex, dragInfo.Member);
        }
        SaveMatchData();
    }

    private async Task SwapMembersWithBreakAsync(DragInfo dragInfo, MatchupRound targetRound, Member targetBreakMember)
    {
        if (dragInfo.IsFromBreakArea)
        {
            int sourceIndex = dragInfo.SourceRound.BreakMembers.IndexOf(dragInfo.Member);
            int targetIndex = targetRound.BreakMembers.IndexOf(targetBreakMember);
            if (sourceIndex >= 0 && targetIndex >= 0)
            {
                dragInfo.SourceRound.BreakMembers[sourceIndex] = targetBreakMember;
                targetRound.BreakMembers[targetIndex] = dragInfo.Member;
            }
        }
        else
        {
            dragInfo.Member.MatchCount--;
            dragInfo.Member.BreakCount++;
            await _database.SaveMemberAsync(dragInfo.Member);
            targetBreakMember.BreakCount--;
            targetBreakMember.MatchCount++;
            await _database.SaveMemberAsync(targetBreakMember);
            SetPlayerToMatchup(dragInfo.SourceMatchup, dragInfo.PlayerIndex, targetBreakMember);
            int breakIndex = targetRound.BreakMembers.IndexOf(targetBreakMember);
            if (breakIndex >= 0) targetRound.BreakMembers[breakIndex] = dragInfo.Member;
        }
        SaveMatchData();
    }

    private void SetPlayerToMatchup(Matchup matchup, int playerIndex, Member newMember)
    {
        switch (playerIndex)
        {
            case 1: matchup.Player1 = newMember; break;
            case 2: matchup.Player2 = newMember; break;
            case 3: matchup.Player3 = newMember; break;
            case 4: matchup.Player4 = newMember; break;
        }
    }
}
