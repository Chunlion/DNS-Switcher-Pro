import { Component, type ErrorInfo, type ReactNode } from 'react';
import { AlertTriangle, RotateCcw } from 'lucide-react';

interface Props {
  children: ReactNode;
}

interface State {
  error: Error | null;
}

export default class ErrorBoundary extends Component<Props, State> {
  state: State = { error: null };
  private readonly childrenNode: ReactNode;

  constructor(props: Props) {
    super(props);
    this.childrenNode = props.children;
  }

  static getDerivedStateFromError(error: Error) {
    return { error };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Renderer crashed:', error, info);
  }

  render() {
    if (!this.state.error) return this.childrenNode;

    return (
      <main className="flex min-h-screen items-center justify-center bg-slate-100 p-6 text-slate-900">
        <section className="w-full max-w-xl rounded-lg border border-red-200 bg-white p-6 shadow-lg">
          <div className="mb-4 flex items-center gap-3 text-red-700">
            <AlertTriangle size={24} />
            <h1 className="text-lg font-semibold">界面加载失败</h1>
          </div>
          <p className="text-sm leading-6 text-slate-600">
            应用遇到了运行时错误，但已经拦截下来，没有再直接白屏。可以点击重新加载；如果仍然出现，请把下面的错误发给我。
          </p>
          <pre className="mt-4 max-h-56 overflow-auto rounded bg-slate-950 p-4 text-xs text-white">
            {this.state.error.message}
            {'\n'}
            {this.state.error.stack}
          </pre>
          <button
            onClick={() => window.location.reload()}
            className="mt-5 flex items-center gap-2 rounded-lg bg-slate-900 px-4 py-2 text-sm font-semibold text-white hover:bg-slate-700"
          >
            <RotateCcw size={16} />
            重新加载
          </button>
        </section>
      </main>
    );
  }
}
