import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class AudioAlertService {
  private audioCtx: AudioContext | null = null;
  private speechQueue: string[] = [];
  private isSpeaking = false;

  readonly audioEnabled = signal<boolean>(true);
  readonly voiceEnabled = signal<boolean>(true);

  constructor() {
    // AudioContext will be lazily initialized on first user interaction
  }

  private getAudioContext(): AudioContext | null {
    if (typeof window === 'undefined') return null;

    if (!this.audioCtx) {
      const AudioContextClass = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      if (AudioContextClass) {
        this.audioCtx = new AudioContextClass();
      }
    }

    if (this.audioCtx && this.audioCtx.state === 'suspended') {
      this.audioCtx.resume().catch(() => {});
    }

    return this.audioCtx;
  }

  toggleAudio(): boolean {
    const next = !this.audioEnabled();
    this.audioEnabled.set(next);
    return next;
  }

  toggleVoice(): boolean {
    const next = !this.voiceEnabled();
    this.voiceEnabled.set(next);
    return next;
  }

  /**
   * Crisp, ascending 3-tone chime for incoming orders in Kitchen Display.
   * C5 (523.25Hz) -> E5 (659.25Hz) -> G5 (783.99Hz)
   */
  playNewOrderChime(): void {
    if (!this.audioEnabled()) return;
    const ctx = this.getAudioContext();
    if (!ctx) return;

    const now = ctx.currentTime;
    const notes = [523.25, 659.25, 783.99];

    notes.forEach((freq, idx) => {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();

      osc.type = 'sine';
      osc.frequency.setValueAtTime(freq, now + idx * 0.12);

      gain.gain.setValueAtTime(0.2, now + idx * 0.12);
      gain.gain.exponentialRampToValueAtTime(0.001, now + idx * 0.12 + 0.35);

      osc.connect(gain);
      gain.connect(ctx.destination);

      osc.start(now + idx * 0.12);
      osc.stop(now + idx * 0.12 + 0.35);
    });
  }

  /**
   * Classic two-tone "ding-dong" chime for TV pickup display when an order is ready.
   * G5 (783.99Hz) -> E5 (659.25Hz)
   */
  playOrderReadyChime(): void {
    if (!this.audioEnabled()) return;
    const ctx = this.getAudioContext();
    if (!ctx) return;

    const now = ctx.currentTime;
    // First tone (Ding)
    const osc1 = ctx.createOscillator();
    const gain1 = ctx.createGain();
    osc1.type = 'triangle';
    osc1.frequency.setValueAtTime(783.99, now);
    gain1.gain.setValueAtTime(0.3, now);
    gain1.gain.exponentialRampToValueAtTime(0.001, now + 0.45);
    osc1.connect(gain1);
    gain1.connect(ctx.destination);
    osc1.start(now);
    osc1.stop(now + 0.45);

    // Second tone (Dong)
    const osc2 = ctx.createOscillator();
    const gain2 = ctx.createGain();
    osc2.type = 'triangle';
    osc2.frequency.setValueAtTime(659.25, now + 0.22);
    gain2.gain.setValueAtTime(0.35, now + 0.22);
    gain2.gain.exponentialRampToValueAtTime(0.001, now + 0.85);
    osc2.connect(gain2);
    gain2.connect(ctx.destination);
    osc2.start(now + 0.22);
    osc2.stop(now + 0.85);
  }

  /**
   * Urgency double-beep for overdue/delayed orders (>10 mins).
   */
  playUrgentAlert(): void {
    if (!this.audioEnabled()) return;
    const ctx = this.getAudioContext();
    if (!ctx) return;

    const now = ctx.currentTime;
    [0, 0.18].forEach((offset) => {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = 'square';
      osc.frequency.setValueAtTime(880, now + offset);
      gain.gain.setValueAtTime(0.15, now + offset);
      gain.gain.exponentialRampToValueAtTime(0.001, now + offset + 0.12);
      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start(now + offset);
      osc.stop(now + offset + 0.12);
    });
  }

  /**
   * Browser-native Text-To-Speech announcement for ready customer tokens.
   * e.g., "Token number 8492, your order is ready for pickup at Counter 1!"
   */
  speakToken(tokenNumber: string, counter: string = 'Counter 1'): void {
    if (!this.audioEnabled() || !this.voiceEnabled()) return;
    if (typeof window === 'undefined' || !('speechSynthesis' in window)) return;

    // First play pleasant pickup chime
    this.playOrderReadyChime();

    // Format clean spoken text (separate digits if purely numeric so TTS says "eight four nine two" or "8492")
    const cleanToken = tokenNumber.replace(/^#/, '').trim();
    const phrase = `Token number ${cleanToken}, your order is ready for pickup at ${counter}!`;

    this.enqueueSpeech(phrase);
  }

  announceCustom(message: string): void {
    if (!this.audioEnabled() || !this.voiceEnabled()) return;
    this.playOrderReadyChime();
    this.enqueueSpeech(message);
  }

  private enqueueSpeech(phrase: string): void {
    this.speechQueue.push(phrase);
    if (!this.isSpeaking) {
      this.processQueue();
    }
  }

  private processQueue(): void {
    if (this.speechQueue.length === 0) {
      this.isSpeaking = false;
      return;
    }

    if (typeof window === 'undefined' || !('speechSynthesis' in window)) {
      this.speechQueue = [];
      this.isSpeaking = false;
      return;
    }

    const phrase = this.speechQueue.shift();
    if (!phrase) {
      this.isSpeaking = false;
      return;
    }

    this.isSpeaking = true;
    const utterance = new SpeechSynthesisUtterance(phrase);
    utterance.rate = 0.95; // Slightly slower, very clear announcement rate
    utterance.pitch = 1.05; // Friendly, clear announcement pitch

    // Choose preferred voice if available
    const voices = window.speechSynthesis.getVoices();
    const englishVoice = voices.find(v => v.lang.startsWith('en-IN')) ||
                         voices.find(v => v.lang.startsWith('en-US')) ||
                         voices.find(v => v.lang.startsWith('en-GB')) ||
                         voices.find(v => v.lang.startsWith('en'));
    if (englishVoice) {
      utterance.voice = englishVoice;
    }

    utterance.onend = () => {
      // Pause 400ms between announcements
      setTimeout(() => {
        this.processQueue();
      }, 400);
    };

    utterance.onerror = () => {
      this.isSpeaking = false;
      this.processQueue();
    };

    // Small delay to allow chime to ring clearly before speaking
    setTimeout(() => {
      window.speechSynthesis.speak(utterance);
    }, 550);
  }
}

