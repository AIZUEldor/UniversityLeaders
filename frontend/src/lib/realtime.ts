"use client";

// Live hints from the SignalR hub /hubs/notifications (official @microsoft/signalr client; the session cookie
// authenticates negotiate and the transport). Events carry ids only; every hint just tells the UI to re-read the REST
// endpoints, which stay the source of truth.

import { HubConnectionBuilder, LogLevel, type HubConnection, type RetryContext } from "@microsoft/signalr";
import { useEffect, useRef } from "react";
import { apiBaseUrl } from "./api";

/** "connected" fires after every (re)connect so callers re-fetch what they may have missed; "local" after own changes. */
export type MessagingHint = "connected" | "notification" | "announcement" | "local";
type Listener = (hint: MessagingHint) => void;

const listeners = new Set<Listener>();
let connection: HubConnection | null = null;
let retryTimer: number | undefined;
let startAttempt = 0;

/** 1 s, 2 s, 4 s ... capped at 30 s; never gives up while someone is subscribed. */
const backoff = (attempt: number): number => Math.min(30_000, 1_000 * 2 ** attempt);

function emit(hint: MessagingHint): void {
  listeners.forEach((listener: Listener) => listener(hint));
}

function connect(): void {
  const base: string = apiBaseUrl();
  if (base === "" || connection !== null || listeners.size === 0) return;
  const hub: HubConnection = new HubConnectionBuilder()
    .withUrl(`${base}/hubs/notifications`, { withCredentials: true })
    .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (context: RetryContext) => backoff(context.previousRetryCount) })
    .configureLogging(LogLevel.Warning)
    .build();
  hub.on("notification", () => emit("notification"));
  hub.on("announcement", () => emit("announcement"));
  hub.onreconnected(() => emit("connected"));
  hub.onclose(() => {
    if (connection !== hub) return; // stopped on purpose by disconnect()
    connection = null;
    retryTimer = window.setTimeout(connect, backoff(startAttempt++));
  });
  connection = hub;
  void start(hub);
}

// Automatic reconnect covers drops after a successful start; a failed initial start (API down, session not ready) is
// retried here.
async function start(hub: HubConnection): Promise<void> {
  try {
    await hub.start();
    startAttempt = 0;
    emit("connected");
  } catch {
    if (connection !== hub) return;
    connection = null;
    retryTimer = window.setTimeout(connect, backoff(startAttempt++));
  }
}

function disconnect(): void {
  window.clearTimeout(retryTimer);
  const hub: HubConnection | null = connection;
  connection = null;
  startAttempt = 0;
  void hub?.stop();
}

/** Tell other mounted views (e.g. the nav badges) that the user just changed read state. */
export function announceLocalChange(): void {
  emit("local");
}

/**
 * Subscribes while `enabled`. One shared connection serves all subscribers and stops with the last one. If it never
 * connects, nothing breaks: views still load and refresh from REST.
 */
export function useMessagingHints(enabled: boolean, onHint: Listener): void {
  const latest = useRef<Listener>(onHint);
  useEffect(() => { latest.current = onHint; });
  useEffect(() => {
    if (!enabled) return;
    const listener: Listener = (hint: MessagingHint) => latest.current(hint);
    listeners.add(listener);
    connect();
    return () => {
      listeners.delete(listener);
      if (listeners.size === 0) disconnect();
    };
  }, [enabled]);
}
