"use client";

import React, { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Grid, Paper, Typography, Box, CircularProgress, Card, CardContent } from '@mui/material';
import {
    People as UserIcon,
    Description as ProposalIcon,
    Public as RegionIcon,
    Category as ServiceIcon
} from '@mui/icons-material';
import { adminApi, proposalApi, catalogApi } from '../services/api';

export default function AdminDashboard() {
    const [stats, setStats] = useState<any>(null);
    const [loading, setLoading] = useState(true);
    const router = useRouter();

    useEffect(() => {
        const fetchStats = async () => {
            try {
                const [users, proposals, regions] = await Promise.all([
                    adminApi.getUsers(),
                    proposalApi.list(),
                    catalogApi.getRegions()
                ]);

                setStats({
                    users: users.data.length,
                    proposals: proposals.data.total,
                    regions: regions.data.length
                });
            } catch (err: any) {
                if (err.response?.status === 401) {
                    localStorage.removeItem('token');
                    router.push('/login');
                }
                console.error(err);
            } finally {
                setLoading(false);
            }
        };
        fetchStats();
    }, []);

    if (loading) return <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}><CircularProgress /></Box>;

    const cards = [
        { title: 'Total Users', value: stats?.users ?? 0, icon: <UserIcon color="primary" />, color: '#e3f2fd' },
        { title: 'Proposals Created', value: stats?.proposals ?? 0, icon: <ProposalIcon color="success" />, color: '#e8f5e9' },
        { title: 'Active Regions', value: stats?.regions ?? 0, icon: <RegionIcon color="secondary" />, color: '#f3e5f5' },
    ];

    return (
        <Box>
            <Typography variant="h4" fontWeight="bold" sx={{ mb: 4 }}>System Overview</Typography>
            <Grid container spacing={3}>
                {cards.map((card) => (
                    <Grid key={card.title} size={{ xs: 12, md: 4 }}>
                        <Card sx={{ backgroundColor: card.color }}>
                            <CardContent>
                                <Box sx={{ display: 'flex', alignItems: 'center', mb: 1 }}>
                                    {card.icon}
                                    <Typography variant="h6" sx={{ ml: 1 }}>{card.title}</Typography>
                                </Box>
                                <Typography variant="h3" fontWeight="bold">{card.value}</Typography>
                            </CardContent>
                        </Card>
                    </Grid>
                ))}
            </Grid>

            <Paper sx={{ mt: 4, p: 3 }}>
                <Typography variant="h6" gutterBottom>Welcome to the Cherry Admin Console</Typography>
                <Typography variant="body1">
                    Use the sidebar to manage regions, services, pricing, templates, and users.
                    Changes made here will affect the Proposal Wizard and document generation for all users.
                </Typography>
            </Paper>
        </Box>
    );
}
