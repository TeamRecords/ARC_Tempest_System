import { NextResponse } from "next/server";
import { fetchPlayerSnapshot } from "@/lib/positions";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

export async function GET() {
  try {
    const snapshot = await fetchPlayerSnapshot();
    return NextResponse.json(snapshot, { headers: { "Cache-Control": "no-store" } });
  } catch (error) {
    console.error("[TempestMap] API error", error);
    return NextResponse.json({ error: "Failed to fetch tactical map" }, { status: 500 });
  }
}
