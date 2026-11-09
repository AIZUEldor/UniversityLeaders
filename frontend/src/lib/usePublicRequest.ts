"use client";

import { useCallback, useEffect, useRef, useState } from "react";

export function usePublicRequest<T>(request: () => Promise<T>) {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);
  const requestNumber = useRef(0);

  const reload = useCallback(async () => {
    const current = ++requestNumber.current;
    setLoading(true);
    try { const result = await request(); if (current === requestNumber.current) { setData(result); setError(null); } }
    catch (failure: unknown) { if (current === requestNumber.current) setError(failure); }
    finally { if (current === requestNumber.current) setLoading(false); }
  }, [request]);
  useEffect(() => {
    const counter = requestNumber; // request counter, not a DOM ref: bumping it discards stale responses
    const timer = window.setTimeout(() => { void reload(); }, 0);
    return () => { window.clearTimeout(timer); counter.current++; };
  }, [reload]);

  return { data, loading, error, reload };
}
