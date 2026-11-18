import { Bell, BellOff, SkipForward } from 'lucide-react';
import { useEffect, useRef } from 'react';
import type { RestState } from './use-rest-timer';
import { formatDuration } from '@/lib/domain';
import { prefKeys, useBoolPref } from '@/lib/prefs';
import { useNow } from '@/lib/use-now';
import { Button } from '@/components/ui/button';

let audio: AudioContext | null = null;

/** Browsers only allow audio after a user gesture, so this is also called from the sound toggle. */
function unlockAudio() {
  try {
    audio ??= new AudioContext();
    if (audio.state === 'suspended') void audio.resume();
  } catch {
    audio = null;
  }
}

function beep() {
  try {
    unlockAudio();
    if (!audio) return;
    const t = audio.currentTime;
    for (const offset of [0, 0.25]) {
      const osc = audio.createOscillator();
      const gain = audio.createGain();
      osc.frequency.value = 880;
      gain.gain.setValueAtTime(0.0001, t + offset);
      gain.gain.exponentialRampToValueAtTime(0.3, t + offset + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.0001, t + offset + 0.2);
      osc.connect(gain).connect(audio.destination);
      osc.start(t + offset);
      osc.stop(t + offset + 0.22);
    }
  } catch {
    // No audio output available.
  }
}

export function RestTimerBar({ state, onAdd, onStop }: { state: RestState; onAdd: (delta: number) => void; onStop: () => void }) {
  const now = useNow(250);
  const [sound, setSound] = useBoolPref(prefKeys.restSound, false);
  const [vibrate] = useBoolPref(prefKeys.restVibrate, true);
  const remaining = Math.max(0, Math.ceil((state.endAt - now) / 1000));
  const finished = remaining === 0;
  const progress = Math.min(1, Math.max(0, 1 - (state.endAt - now) / (state.total * 1000)));
  const onStopRef = useRef(onStop);
  useEffect(() => {
    onStopRef.current = onStop;
  });

  useEffect(() => {
    if (!finished) return;
    if (vibrate && 'vibrate' in navigator) navigator.vibrate([200, 100, 200]);
    if (sound) beep();
    const t = setTimeout(() => onStopRef.current(), 4000);
    return () => clearTimeout(t);
  }, [finished, sound, vibrate]);

  return (
    <div className="border-t border-border bg-surface px-4 pt-3 pb-3 shadow-[0_-4px_12px_rgb(0_0_0/0.06)]">
      <div className="mx-auto flex max-w-2xl items-center gap-3">
        <div className="min-w-0 flex-1">
          <div className="flex items-baseline gap-2">
            <span className="text-xs font-medium tracking-wide text-muted uppercase">{finished ? 'Rest over' : 'Rest'}</span>
            <span role="timer" aria-label="Rest remaining" className={finished ? 'tabular text-3xl font-semibold text-primary' : 'tabular text-3xl font-semibold'}>
              {formatDuration(remaining)}
            </span>
          </div>
          <div className="mt-1.5 h-1.5 overflow-hidden rounded-full bg-surface-2" aria-hidden>
            <div className="h-full rounded-full bg-primary transition-[width] duration-200 ease-linear" style={{ width: `${progress * 100}%` }} />
          </div>
        </div>
        <Button variant="secondary" size="icon" aria-label="15 seconds less rest" onClick={() => onAdd(-15)} disabled={finished}>
          <span className="text-sm font-semibold">−15</span>
        </Button>
        <Button variant="secondary" size="icon" aria-label="15 seconds more rest" onClick={() => onAdd(15)}>
          <span className="text-sm font-semibold">+15</span>
        </Button>
        <Button
          variant="ghost"
          size="icon"
          aria-label="Sound when rest ends"
          aria-pressed={sound}
          onClick={() => {
            if (!sound) unlockAudio();
            setSound(!sound);
          }}
        >
          {sound ? <Bell /> : <BellOff />}
        </Button>
        <Button variant={finished ? 'primary' : 'secondary'} onClick={onStop} aria-label={finished ? 'Dismiss rest timer' : 'Skip rest'}>
          <SkipForward aria-hidden />
          <span className="max-sm:sr-only">{finished ? 'Go' : 'Skip'}</span>
        </Button>
      </div>
      <p className="sr-only" aria-live="assertive">
        {finished ? 'Rest over. Time for the next set.' : ''}
      </p>
    </div>
  );
}
