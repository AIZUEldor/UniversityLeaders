/** Decorative animated page background (see .animated-bg in globals.css). Hidden from assistive technology. */
export function AnimatedBackground() {
  return <div className="animated-bg" aria-hidden="true">
    <div className="animated-bg__dots" />
    <div className="animated-bg__blob animated-bg__blob--blue" />
    <div className="animated-bg__blob animated-bg__blob--white" />
    <div className="animated-bg__blob animated-bg__blob--green" />
  </div>;
}
