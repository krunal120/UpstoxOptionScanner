export interface IndexDefinition {
  key: string;
  name: string;
  shortName: string;
  exchange: string;
  lotSize: number;
  strikeStep: number;
}

export const SUPPORTED_INDICES: IndexDefinition[] = [
  {
    key: 'NSE_INDEX|Nifty 50',
    name: 'NIFTY 50',
    shortName: 'NIFTY',
    exchange: 'NSE',
    lotSize: 65,
    strikeStep: 50,
  },
  {
    key: 'NSE_INDEX|Nifty Bank',
    name: 'BANK NIFTY',
    shortName: 'BANKNIFTY',
    exchange: 'NSE',
    lotSize: 30,
    strikeStep: 100,
  },
  {
    key: 'BSE_INDEX|SENSEX',
    name: 'BSE SENSEX',
    shortName: 'SENSEX',
    exchange: 'BSE',
    lotSize: 20,
    strikeStep: 100,
  },
];

export const getIndexDefinition = (key?: string): IndexDefinition => {
  return SUPPORTED_INDICES.find((i) => i.key === key) || SUPPORTED_INDICES[0];
};
