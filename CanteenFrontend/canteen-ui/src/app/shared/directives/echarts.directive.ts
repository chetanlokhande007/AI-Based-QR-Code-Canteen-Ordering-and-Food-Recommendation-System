import {
  Directive,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
  OnInit,
  SimpleChanges,
  inject
} from '@angular/core';
import * as echarts from 'echarts';

@Directive({
  selector: '[appEcharts]',
  standalone: true
})
export class EchartsDirective implements OnInit, OnChanges, OnDestroy {
  private readonly el = inject(ElementRef);
  private chart: echarts.ECharts | null = null;
  private resizeObserver: ResizeObserver | null = null;

  @Input('appEcharts') options: echarts.EChartsOption | null = null;
  @Input() theme: string | object = 'light';

  ngOnInit(): void {
    this.initChart();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['options'] && this.chart) {
      if (this.options) {
        this.chart.setOption(this.options, true);
      } else {
        this.chart.clear();
      }
    } else if (changes['options'] && !this.chart && this.options) {
      this.initChart();
    }
  }

  private initChart(): void {
    if (typeof window === 'undefined' || !this.el.nativeElement) return;

    if (this.chart) {
      this.chart.dispose();
      this.chart = null;
    }

    try {
      const clientWidth = this.el.nativeElement.clientWidth || 600;
      const clientHeight = this.el.nativeElement.clientHeight || 300;

      this.chart = echarts.init(this.el.nativeElement, this.theme, {
        renderer: 'svg',
        width: clientWidth,
        height: clientHeight
      });

      if (this.options) {
        this.chart.setOption(this.options, true);
      }

      if (typeof ResizeObserver !== 'undefined') {
        this.resizeObserver = new ResizeObserver(() => {
          try {
            this.chart?.resize();
          } catch {
            // ignore resize errors in tests
          }
        });
        this.resizeObserver.observe(this.el.nativeElement);
      }
    } catch (e) {
      // Graceful fallback for non-browser or mock environments
    }
  }

  ngOnDestroy(): void {
    if (this.resizeObserver) {
      this.resizeObserver.disconnect();
      this.resizeObserver = null;
    }
    if (this.chart) {
      try {
        this.chart.dispose();
      } catch {
        // ignore
      }
      this.chart = null;
    }
  }
}
