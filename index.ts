import Anthropic from "@anthropic-ai/sdk";
import "dotenv/config";

const apiKey = process.env.CLAUDE_API_KEY;

const client = new Anthropic({
  apiKey,
});

const message = await client.messages.create({
  //   model: "claude-opus-5-5",
  model: "claude-haiku-5-5",
  max_tokens: 1024,
  messages: [{ role: "user", content: "Hello, world" }],
});

console.log(message.content);
