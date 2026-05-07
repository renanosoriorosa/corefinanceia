import { NextRequest, NextResponse } from 'next/server';

const API_URL = process.env.API_URL ?? 'http://localhost:5176';

type Params = Promise<{ path: string[] }>;

async function proxy(req: NextRequest, params: Params): Promise<NextResponse> {
  const { path } = await params;
  const targetUrl = `${API_URL}/api/${path.join('/')}${req.nextUrl.search}`;

  const hasBody = req.method !== 'GET' && req.method !== 'HEAD';

  const response = await fetch(targetUrl, {
    method: req.method,
    headers: { 'Content-Type': 'application/json' },
    body: hasBody ? await req.text() : undefined,
  });

  const body = await response.text();

  return new NextResponse(body, {
    status: response.status,
    headers: { 'Content-Type': 'application/json' },
  });
}

export async function GET(req: NextRequest, { params }: { params: Params }) {
  return proxy(req, params);
}
export async function POST(req: NextRequest, { params }: { params: Params }) {
  return proxy(req, params);
}
export async function PUT(req: NextRequest, { params }: { params: Params }) {
  return proxy(req, params);
}
export async function DELETE(req: NextRequest, { params }: { params: Params }) {
  return proxy(req, params);
}
