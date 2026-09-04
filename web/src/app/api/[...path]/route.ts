import { NextRequest, NextResponse } from 'next/server';

const API_URL = process.env.API_URL ?? 'http://localhost:5176';

/** Status que, pela spec do Fetch, não podem ter corpo na resposta. */
const STATUS_SEM_CORPO = [204, 205, 304];

type Params = Promise<{ path: string[] }>;

async function proxy(req: NextRequest, params: Params): Promise<NextResponse> {
  const { path } = await params;
  const targetUrl = `${API_URL}/api/${path.join('/')}${req.nextUrl.search}`;

  const hasBody = req.method !== 'GET' && req.method !== 'HEAD';

  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  const authorization = req.headers.get('authorization');
  if (authorization) {
    headers.Authorization = authorization;
  }

  const response = await fetch(targetUrl, {
    method: req.method,
    headers,
    body: hasBody ? await req.text() : undefined,
  });

  if (STATUS_SEM_CORPO.includes(response.status)) {
    return new NextResponse(null, { status: response.status });
  }

  const body = await response.text();

  if (body.length === 0) {
    return new NextResponse(null, { status: response.status });
  }

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
