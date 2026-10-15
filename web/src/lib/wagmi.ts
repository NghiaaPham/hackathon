import { createConfig, http, injected } from "wagmi";
import { avalancheFuji, anvil } from "viem/chains";

export const wagmiConfig = createConfig({
  chains: [avalancheFuji, anvil],
  connectors: [injected()],
  transports: {
    [avalancheFuji.id]: http(process.env.NEXT_PUBLIC_FUJI_RPC_URL ?? "https://api.avax-test.network/ext/bc/C/rpc"),
    [anvil.id]: http("http://127.0.0.1:8545"),
  },
  ssr: true,
});
