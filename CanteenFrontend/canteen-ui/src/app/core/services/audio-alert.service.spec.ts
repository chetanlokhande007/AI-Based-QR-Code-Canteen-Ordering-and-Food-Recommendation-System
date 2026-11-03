import { TestBed } from '@angular/core/testing';
import { AudioAlertService } from './audio-alert.service';

describe('AudioAlertService', () => {
  let service: AudioAlertService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [AudioAlertService]
    });
    service = TestBed.inject(AudioAlertService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
    expect(service.audioEnabled()).toBe(true);
    expect(service.voiceEnabled()).toBe(true);
  });

  it('should toggle audio and voice settings', () => {
    const audioState = service.toggleAudio();
    expect(audioState).toBe(false);
    expect(service.audioEnabled()).toBe(false);

    const voiceState = service.toggleVoice();
    expect(voiceState).toBe(false);
    expect(service.voiceEnabled()).toBe(false);
  });

  it('should not throw when triggering chimes in test environment', () => {
    expect(() => service.playNewOrderChime()).not.toThrow();
    expect(() => service.playOrderReadyChime()).not.toThrow();
    expect(() => service.playUrgentAlert()).not.toThrow();
  });

  it('should not throw when speaking token', () => {
    expect(() => service.speakToken('101', 'Counter 1')).not.toThrow();
  });
});
