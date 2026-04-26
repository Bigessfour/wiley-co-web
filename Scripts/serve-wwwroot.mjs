import { createReadStream, existsSync, statSync } from "node:fs";
import { createServer } from "node:http";
import { extname, resolve, join, sep } from "node:path";

const defaultRoot = "./publish_output/wwwroot";
const defaultPort = 5230;

const args = process.argv.slice(2);
let root = defaultRoot;
let port = defaultPort;

for (let index = 0; index < args.length; index += 1) {
  const value = args[index];

  if (value === "--root" && args[index + 1]) {
    root = args[index + 1];
    index += 1;
    continue;
  }

  if (value === "--port" && args[index + 1]) {
    port = Number(args[index + 1]);
    index += 1;
  }
}

const rootDirectory = resolve(process.cwd(), root);
const indexFile = join(rootDirectory, "index.html");

if (!existsSync(indexFile)) {
  throw new Error(`Static root not found: ${indexFile}`);
}

const contentTypes = new Map([
  [".css", "text/css; charset=utf-8"],
  [".html", "text/html; charset=utf-8"],
  [".ico", "image/x-icon"],
  [".jpeg", "image/jpeg"],
  [".jpg", "image/jpeg"],
  [".js", "application/javascript; charset=utf-8"],
  [".json", "application/json; charset=utf-8"],
  [".map", "application/json; charset=utf-8"],
  [".png", "image/png"],
  [".svg", "image/svg+xml"],
  [".txt", "text/plain; charset=utf-8"],
  [".woff", "font/woff"],
  [".woff2", "font/woff2"],
]);

const writeFile = (response, filePath) => {
  const extension = extname(filePath).toLowerCase();
  response.statusCode = 200;
  response.setHeader(
    "Content-Type",
    contentTypes.get(extension) ?? "application/octet-stream",
  );
  createReadStream(filePath).pipe(response);
};

const rootPrefix = `${rootDirectory}${sep}`;
const isInsideRoot = (candidatePath) =>
  candidatePath === rootDirectory || candidatePath.startsWith(rootPrefix);

const server = createServer((request, response) => {
  const requestUrl = new URL(request.url ?? "/", "http://localhost");
  const relativePath = decodeURIComponent(requestUrl.pathname);
  const candidatePath = resolve(rootDirectory, `.${relativePath}`);

  if (!isInsideRoot(candidatePath)) {
    response.statusCode = 403;
    response.end("Forbidden");
    return;
  }

  if (existsSync(candidatePath) && statSync(candidatePath).isFile()) {
    writeFile(response, candidatePath);
    return;
  }

  if (relativePath === "/" || extname(relativePath) === "") {
    writeFile(response, indexFile);
    return;
  }

  response.statusCode = 404;
  response.end("Not found");
});

server.listen(port, "0.0.0.0", () => {
  console.log(`[serve-wwwroot] ${rootDirectory} -> http://0.0.0.0:${port}`);
});
