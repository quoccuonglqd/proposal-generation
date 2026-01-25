"use client";

import Link from 'next/link';
import {
  Box,
  Container,
  Typography,
  Grid,
  Card,
  CardContent,
  CardActionArea,
  Stack
} from '@mui/material';
import {
  AddCircleOutline as CreateIcon,
  Settings as AdminIcon,
  Description as ProposalIcon
} from '@mui/icons-material';

import { useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';

export default function Home() {
  const router = useRouter();
  const [user, setUser] = useState<any>(null);

  useEffect(() => {
    const storedUser = localStorage.getItem('user');
    if (!storedUser) {
      router.push('/login');
    } else {
      setUser(JSON.parse(storedUser));
    }
  }, [router]);

  if (!user) return null;

  const isAdmin = user.roles?.includes('Admin');
  const isCreatorFull = user.roles?.includes('ProposalCreator') || isAdmin;

  const cards = [
    {
      title: 'Create Proposal',
      description: 'Start a new proposal wizard with region and service selection.',
      icon: <CreateIcon sx={{ fontSize: 40 }} />,
      link: '/proposals/new',
      color: 'primary.main',
      visible: isCreatorFull
    },
    {
      title: 'View Proposals',
      description: 'Review and manage existing proposals and their versions.',
      icon: <ProposalIcon sx={{ fontSize: 40 }} />,
      link: '/proposals',
      color: 'success.main',
      visible: true
    },
    {
      title: 'Admin Console',
      description: 'Manage services, pricing, regions, and templates.',
      icon: <AdminIcon sx={{ fontSize: 40 }} />,
      link: '/admin',
      color: 'secondary.main',
      visible: isAdmin
    },
  ].filter(card => card.visible);

  return (
    <Container maxWidth="lg">
      <Box sx={{ py: 8 }}>
        <Typography variant="h3" component="h1" gutterBottom align="center" fontWeight="bold">
          Cherry Project
        </Typography>
        <Typography variant="h6" align="center" color="text.secondary" paragraph sx={{ mb: 6 }}>
          Centralized Proposal Generation Tool
        </Typography>

        <Grid container spacing={4}>
          {cards.map((card) => (
            <Grid size={{ xs: 12, md: 4 }} key={card.title}>
              <Card sx={{ height: '100%' }}>
                <CardActionArea component={Link} href={card.link} sx={{ height: '100%', p: 2 }}>
                  <CardContent>
                    <Stack spacing={2} alignItems="center">
                      <Box sx={{ color: card.color }}>
                        {card.icon}
                      </Box>
                      <Typography variant="h5" component="h2" fontWeight="medium">
                        {card.title}
                      </Typography>
                      <Typography variant="body1" color="text.secondary" align="center">
                        {card.description}
                      </Typography>
                    </Stack>
                  </CardContent>
                </CardActionArea>
              </Card>
            </Grid>
          ))}
        </Grid>
      </Box>
    </Container>
  );
}
