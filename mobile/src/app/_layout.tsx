import { Stack } from "expo-router";
import { SQLiteProvider } from "expo-sqlite";
import { migrate } from "../telemetry/sqliteOutbox";

export default function RootLayout() {
  return (
    <SQLiteProvider databaseName="fleetpulse.db" onInit={migrate}>
      <Stack screenOptions={{ headerShown: false }} />
    </SQLiteProvider>
  );
}
