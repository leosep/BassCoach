using BassCoach.Core.Audio;
using BassCoach.Core.Dsp;
using BassCoach.Core.Practice;
using BassCoach.Core.Storage;
using BassCoach.Core.Theory;

namespace BassCoach.SelfTest;

public static class CoreTests
{
    public static void RunAll()
    {
        Console.WriteLine("Theory");
        T.Run("note math round trip", () =>
        {
            T.Near(28, NoteMath.FrequencyToMidi(41.2034), 0.01, "E1 midi");
            T.Near(55.0, NoteMath.MidiToFrequency(33), 0.01, "A1 hz");
            T.Equal("E1", NoteMath.NameWithOctave(28), "name");
            T.Equal("Bb", NoteMath.Name(10, flats: true), "flat name");
            T.True(NoteMath.KeyUsesFlats(5, false) && !NoteMath.KeyUsesFlats(7, false), "key spelling");
        });
        T.Run("chord roles", () =>
        {
            var am7 = new Chord(9, ChordQuality.Minor7);
            T.Equal(NoteRole.Third, am7.RoleOf(0), "C over Am7");
            T.Equal(NoteRole.Seventh, am7.RoleOf(7), "G over Am7");
            T.Equal(NoteRole.ScaleTone, am7.RoleOf(11), "B over Am7 (dorian)");
            T.Equal(NoteRole.Outside, am7.RoleOf(10), "Bb over Am7");
            T.Equal("Am7", am7.Symbol(), "symbol");
            T.Equal("Cmaj7", new Chord(0, ChordQuality.Major7).Symbol(), "symbol maj7");
            T.Equal(NoteRole.Fifth, new Chord(11, ChordQuality.Diminished).RoleOf(5), "dim fifth");
        });
        T.Run("progression library", () =>
        {
            T.Equal(12, ProgressionLibrary.Find("12-bar blues").Build(4).BarCount, "blues bars");
            var pop = ProgressionLibrary.Find("Pop I-V-vi-IV").Build(7);
            T.Equal("G D Em C", string.Join(" ", pop.Bars.Select(c => c.Symbol())), "pop in G");
            var salsa = ProgressionLibrary.Find("Salsa i-iv-V7-i").Build(9);
            T.Equal("Am Dm E7 Am", string.Join(" ", salsa.Bars.Select(c => c.Symbol())), "salsa in Am");
            foreach (var template in ProgressionLibrary.All)
                T.True(template.Build(0).BarCount >= 2, template.Name + " has bars");
        });
        T.Run("generated progressions are valid and deterministic", () =>
        {
            for (int seed = 0; seed < 50; seed++)
                foreach (bool minor in new[] { false, true })
                {
                    var chart = ProgressionLibrary.Generate(new Random(seed), 2, minor, 8);
                    T.Equal(8, chart.BarCount, "bars");
                    T.Equal(2, chart.Bars[0].RootPc, "starts on tonic");
                    T.True(chart.Bars[^1].RootPc is 9 or 2, "ends on dominant or tonic");
                }
            var a = ProgressionLibrary.Generate(new Random(5), 0, false, 4).Bars;
            var b = ProgressionLibrary.Generate(new Random(5), 0, false, 4).Bars;
            T.True(a.SequenceEqual(b), "same seed, same chart");
        });
        T.Run("fretboard markers", () =>
        {
            var markers = Fretboard.ChordToneMarkers(BassTuning.Standard4, new Chord(4, ChordQuality.Major));
            T.True(markers.Any(m => m.String == 0 && m.Fret == 0 && m.Role == NoteRole.Root), "open E is the root");
            T.True(markers.Any(m => m.String == 1 && m.Fret == 2 && m.Role == NoteRole.Fifth), "B on the A string is the fifth");
            T.True(markers.All(m => NoteMath.PitchClass(BassTuning.Standard4.OpenStringMidi[m.String] + m.Fret) == m.PitchClass), "pitch classes consistent");
        });

        Console.WriteLine("Pitch detection");
        T.Run("YIN finds bass notes at 44.1 and 48 kHz", () =>
        {
            foreach (int sr in new[] { 44100, 48000 })
                foreach (double hz in new[] { 30.87, 41.2, 55.0, 98.0, 196.0, 392.0 })
                {
                    var detector = new PitchDetector(sr, 28);
                    var frame = SynthBass.Note(sr, hz, 0.5, decayPerSecond: 0);
                    // Skip the 5 ms attack ramp, as the note tracker does (it analyses 80 ms after the onset).
                    var estimate = detector.Detect(frame.AsSpan(sr / 20, detector.RequiredSamples));
                    T.True(estimate.IsVoiced, $"{hz} Hz voiced at {sr}");
                    // Note identification, not tuner grade: the 5-string low B is allowed 1 % (~17 cents) of error.
                    T.Near(hz, estimate.FrequencyHz, hz * (hz < 35 ? 0.01 : 0.005), $"{hz} Hz at {sr}");
                }
        });
        T.Run("YIN treats noise-free silence as unvoiced", () =>
        {
            var detector = new PitchDetector(48000, 36);
            T.True(!detector.Detect(new float[detector.RequiredSamples]).IsVoiced, "silence");
        });

        Console.WriteLine("Note tracking");
        T.Run("detects notes, pitch and onset time", () =>
        {
            const int sr = 48000;
            var script = new (double Start, int Midi, double Length)[] { (0.5, 28, 0.45), (1.0, 33, 0.45), (1.5, 38, 0.45), (2.0, 28, 0.45), (2.5, 40, 0.45) };
            var audio = SynthBass.Mix(sr, 3.4, script);
            var tracker = new NoteTracker(sr, 36);
            var events = new List<NoteEvent>();
            tracker.NoteDetected += events.Add;
            for (int i = 0; i < audio.Length; i += 480) tracker.Process(audio.AsSpan(i, Math.Min(480, audio.Length - i)));
            T.Equal(script.Length, events.Count, "note count");
            for (int i = 0; i < script.Length; i++)
            {
                T.Equal(script[i].Midi, events[i].Midi, $"note {i} pitch");
                T.Near(script[i].Start, events[i].OnsetSeconds, 0.020, $"note {i} onset"); // well inside the 50 ms "perfect" window
            }
        });
        T.Run("handles notes that ring into each other", () =>
        {
            const int sr = 44100;
            var audio = SynthBass.Mix(sr, 2.4, new (double, int, double)[] { (0.4, 28, 1.0), (0.8, 33, 1.0), (1.2, 28, 1.0) });
            var tracker = new NoteTracker(sr, 36);
            var midis = new List<int>();
            var detail = new List<string>();
            tracker.NoteDetected += e => { midis.Add(e.Midi); detail.Add($"{e.Midi}@{e.OnsetSeconds:0.000}"); };
            for (int i = 0; i < audio.Length; i += 441) tracker.Process(audio.AsSpan(i, Math.Min(441, audio.Length - i)));
            T.True(midis.SequenceEqual(new[] { 28, 33, 28 }), "notes: " + string.Join(" ", detail));
        });
        T.Run("ignores noise below the gate", () =>
        {
            var rng = new Random(1);
            var noise = Enumerable.Range(0, 48000 * 2).Select(_ => (float)((rng.NextDouble() - 0.5) * 0.004)).ToArray();
            var tracker = new NoteTracker(48000, 36, gate: 0.01);
            int count = 0;
            tracker.NoteDetected += _ => count++;
            tracker.Process(noise);
            T.Equal(0, count, "events from noise");
        });

        Console.WriteLine("Backing band");
        T.Run("every style renders clean, non-silent audio", () =>
        {
            foreach (var style in StyleCatalog.All)
            {
                var template = ProgressionLibrary.For(style).First();
                var chart = template.Build(0);
                var synth = new BackingSynth(new BackingOptions(48000, 100, style, chart, 1, BassGuide: true));
                var buffer = new float[48000 * 3];
                synth.Read(buffer);
                T.True(buffer.All(float.IsFinite), style + " finite");
                T.True(buffer.Max(Math.Abs) <= 1.0f, style + " within range");
                double energy = Math.Sqrt(buffer.Sum(x => (double)x * x) / buffer.Length);
                T.True(energy > 0.01, $"{style} is audible (rms {energy:0.000})");
            }
        });
        T.Run("beat clock is sample accurate and finishes", () =>
        {
            var chart = ProgressionLibrary.Find("Rock I-IV-V-IV").Build(0);
            var synth = new BackingSynth(new BackingOptions(48000, 120, Style.Rock, chart, 1));
            var block = new float[24000];
            synth.Read(block); // 0.5 s = 1 beat at 120 BPM
            T.Near(-3.0, synth.CurrentBeat, 1e-6, "beat after one beat of count-in");
            for (int i = 0; i < 3; i++) synth.Read(block);
            T.Near(0.0, synth.CurrentBeat, 1e-6, "beat after count-in");
            T.True(!synth.Finished, "not finished yet");
            for (int i = 0; i < 20; i++) synth.Read(block);
            T.True(synth.Finished, "finished after the loops");
        });
        T.Run("count-in has clicks and no chords", () =>
        {
            var chart = ProgressionLibrary.Find("Rock I-IV-V-IV").Build(0);
            var synth = new BackingSynth(new BackingOptions(48000, 120, Style.Rock, chart, 1));
            var countIn = new float[48000 * 2 - 100];
            synth.Read(countIn);
            T.True(countIn.Take(2000).Max(Math.Abs) > 0.05f, "first click audible");
            T.True(countIn.Skip(10000).Take(10000).Max(Math.Abs) < 0.01f, "silence between clicks");
        });
        T.Run("rejects unusable tempos", () =>
        {
            var chart = ProgressionLibrary.Find("Vamp I-IV").Build(0);
            bool threw = false;
            try { _ = new BackingSynth(new BackingOptions(48000, 5, Style.Rock, chart, 1)); } catch (ArgumentOutOfRangeException) { threw = true; }
            T.True(threw, "bpm 5 rejected");
        });

        Console.WriteLine("Evaluation");
        var plan = new PracticePlan(Style.Pop, ProgressionLibrary.Find("Pop I-V-vi-IV").Build(7), ExerciseKind.Roots, 120, 1);
        NoteEvent At(double beat, int midi, double offsetMs = 0) =>
            new((beat + 4) * 0.5 + offsetMs / 1000.0, midi, 0, 440, 0.5); // count-in = 4 beats, 0.5 s per beat
        T.Run("roots: hit, wrong note, missed and extra", () =>
        {
            var evaluator = new PerformanceEvaluator(plan, 0);
            var hit = evaluator.Evaluate(At(0, 43, +20))!;                 // G on beat 1 of G
            T.Equal(NoteOutcome.Hit, hit.Outcome, "root hit");
            T.Near(20, hit.TimingMs, 0.5, "timing");
            T.Equal(TimingRating.Perfect, hit.Rating, "rating");
            T.Equal(NoteOutcome.WrongNote, evaluator.Evaluate(At(4, 40))!.Outcome, "E over D");
            T.Equal(NoteOutcome.Hit, evaluator.Evaluate(At(4.1, 38, -90))!.Outcome, "D, early but accepted");
            T.Equal(NoteOutcome.Extra, evaluator.Evaluate(At(10.5, 40))!.Outcome, "note between targets");
            T.True(evaluator.AdvanceTo(30).Count == 2, "bars 3 and 4 missed");
            var summary = evaluator.BuildSummary();
            T.Equal(2, summary.TargetsHit, "hits");
            T.Near(0.5, summary.Accuracy, 1e-9, "accuracy");
            T.Equal(1, summary.WrongNotes, "wrong");
            T.Equal(1, summary.ExtraNotes, "extra");
            T.Equal(2, summary.MissedTargets, "missed");
        });
        T.Run("latency compensation shifts timing", () =>
        {
            var evaluator = new PerformanceEvaluator(plan, 80);
            var result = evaluator.Evaluate(At(0, 43, +100))!;
            T.Near(20, result.TimingMs, 0.5, "100 ms heard - 80 ms latency");
        });
        T.Run("chord tones exercise accepts any chord tone after the root", () =>
        {
            var chordTones = plan with { Kind = ExerciseKind.ChordTones };
            var evaluator = new PerformanceEvaluator(chordTones, 0);
            T.Equal(NoteOutcome.Hit, evaluator.Evaluate(At(0, 43))!.Outcome, "G root");
            T.Equal(NoteOutcome.Hit, evaluator.Evaluate(At(1, 47))!.Outcome, "B third");
            T.Equal(NoteOutcome.WrongNote, evaluator.Evaluate(At(2, 44))!.Outcome, "G# is outside");
            T.Equal(16, chordTones.BuildTargets().Count, "4 targets per bar");
        });
        T.Run("free groove judges notes by chord and scale", () =>
        {
            var free = plan with { Kind = ExerciseKind.FreeGroove };
            var evaluator = new PerformanceEvaluator(free, 0);
            T.Equal(NoteOutcome.Hit, evaluator.Evaluate(At(0, 45))!.Outcome, "A is a scale tone of G");
            T.Equal(NoteOutcome.WrongNote, evaluator.Evaluate(At(1, 44))!.Outcome, "G# is outside G major");
            T.True(evaluator.Evaluate(At(-3, 43)) is null, "count-in notes ignored");
        });
        T.Run("root and fifth targets", () =>
        {
            var targets = (plan with { Kind = ExerciseKind.RootAndFifth }).BuildTargets();
            T.Equal(8, targets.Count, "two per bar");
            T.Equal(2, targets[1].Beat, "fifth on beat 3");
            T.Equal(2, targets[1].AllowedPitchClasses[0], "D is the fifth of G");
        });
        T.Run("stars and advice", () =>
        {
            var late = new SessionSummary(0.95, 70, 60, 20, 19, 20, 1, 0, 1, 2, true, new Dictionary<NoteRole, int>());
            var tips = PracticeAdvisor.Tips(late, ExerciseKind.Roots);
            T.True(tips.Any(t => t.Contains("late")), "mentions lateness");
            T.True(PracticeAdvisor.Tips(late with { NotesPlayed = 0 }, ExerciseKind.Roots)[0].Contains("did not hear"), "no input advice");
        });

        Console.WriteLine("Full session (simulated bassist)");
        PracticeSession RunSession(Lesson lesson, Func<int, double, (int Midi, double OffsetMs)?> player, out SessionSummary summary)
        {
            const int sr = 48000;
            var settings = new AppSettings { LatencyMs = 0 };
            var sessionPlan = lesson.ToPlan() with { Loops = 2 };
            var session = new PracticeSession(sessionPlan, settings, sr, sr);
            var notes = new List<(double, int, double)>();
            double beatSeconds = 60.0 / sessionPlan.Bpm;
            for (int bar = 0; bar < sessionPlan.Chart.BarCount * sessionPlan.Loops; bar++)
            {
                var choice = player(bar, beatSeconds);
                if (choice is { } c)
                    notes.Add(((sessionPlan.CountInBars * 4 + bar * 4) * beatSeconds + c.OffsetMs / 1000.0, c.Midi, beatSeconds * 0.9));
            }
            double total = (sessionPlan.CountInBars * 4 + sessionPlan.SongBeats + 3) * beatSeconds;
            var input = SynthBass.Mix(sr, total, notes);
            var backing = new float[480];
            for (int i = 0; i + 480 <= input.Length; i += 480)
            {
                session.Backing.Read(backing);
                session.FeedInput(input.AsSpan(i, 480));
                session.GetSnapshot();
            }
            summary = session.Summarize();
            return session;
        }
        var lesson1 = Curriculum.All[0]; // E vamp: E then A, roots
        T.Run("steady bassist passes lesson 1 with 25 ms late bias", () =>
        {
            RunSession(lesson1, (bar, _) => (bar % 2 == 0 ? 28 : 33, 25), out var summary);
            T.True(summary.Accuracy >= 0.95, $"accuracy {summary.Accuracy:0.00}");
            T.Near(25, summary.BiasMs, 12, "bias");
            T.True(summary.Passed && summary.Stars >= 2, $"stars {summary.Stars}");
            T.True(PracticeAdvisor.Tips(summary, lesson1.Kind).Count > 0, "has advice");
        });
        T.Run("sloppy bassist is not passed", () =>
        {
            RunSession(lesson1, (bar, _) => bar % 2 == 0 ? (31, 0) : null, out var summary); // wrong note, half the bars silent
            T.True(summary.Accuracy < 0.5, $"accuracy {summary.Accuracy:0.00}");
            T.True(!summary.Passed, "must not pass");
            T.True(summary.WrongNotes > 0 && summary.MissedTargets > 0, "reports wrong and missed notes");
        });
        T.Run("snapshot phases and chord display", () =>
        {
            var settings = new AppSettings();
            var session = new PracticeSession(lesson1.ToPlan(), settings, 48000, 48000);
            var first = session.GetSnapshot();
            T.Equal(SessionPhase.CountIn, first.Phase, "starts in count-in");
            T.Equal(4, first.CountInBeatsLeft, "four beats left");
            var buffer = new float[48000 * 5];
            session.Backing.Read(buffer);
            var playing = session.GetSnapshot();
            T.Equal(SessionPhase.Playing, playing.Phase, "playing after count-in");
            T.Equal("E", playing.CurrentChord.Symbol(), "E first");
            T.Equal("A", playing.NextChord.Symbol(), "A next");
        });

        Console.WriteLine("Storage");
        T.Run("settings and stats survive a round trip; corrupt files fall back", () =>
        {
            string dir = Path.Combine(Path.GetTempPath(), "bc-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new JsonFileStore<AppSettings>(Path.Combine(dir, "settings.json"));
                store.Save(new AppSettings { LatencyMs = 123, StringCount = 5, InputDeviceId = "abc" });
                var loaded = store.Load();
                T.Equal(123.0, loaded.LatencyMs, "latency");
                T.Equal(5, loaded.StringCount, "strings");
                File.WriteAllText(store.Path, "{ not json");
                T.Equal(90.0, store.Load().LatencyMs, "corrupt file gives defaults");
                T.True(new AppSettings { Sensitivity = 1 }.Gate < new AppSettings { Sensitivity = 0 }.Gate, "higher sensitivity, lower gate");
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        });
        T.Run("lessons unlock in order", () =>
        {
            var stats = new StatsService(new PracticeStats());
            T.True(stats.IsUnlocked(Curriculum.All[0]) && !stats.IsUnlocked(Curriculum.All[1]), "only first unlocked");
            var good = new SessionSummary(0.95, 40, 0, 20, 19, 20, 0, 0, 1, 3, true, new Dictionary<NoteRole, int>());
            stats.Record(Curriculum.All[0].ToPlan(), good, DateTime.UtcNow);
            T.True(stats.IsUnlocked(Curriculum.All[1]), "second unlocked");
            T.Equal("l02", stats.NextLesson()!.Id, "next lesson");
            stats.Record(Curriculum.All[0].ToPlan(), good with { Stars = 1, Accuracy = 0.6 }, DateTime.UtcNow);
            T.Equal(3, stats.BestStars("l01"), "best stars kept");
            T.Equal(2, stats.Stats.Lessons["l01"].Attempts, "attempts counted");
        });
        T.Run("every lesson builds a valid plan", () =>
        {
            foreach (var lesson in Curriculum.All)
            {
                var p = lesson.ToPlan();
                T.True(p.SongBeats > 0 && (lesson.Kind == ExerciseKind.FreeGroove || p.BuildTargets().Count > 0), lesson.Id);
            }
        });
    }
}
